using System.Text;
using System.Text.Json;
using IRETP.Application.Interfaces;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using IRETP.Infrastructure.Services.Rag;

namespace IRETP.Infrastructure.Services;

public class AIOrchestrator : IAIOrchestrator
{
    private readonly IRepository<Transaction> _transactionRepo;
    private readonly IRepository<Zone> _zoneRepo;
    private readonly IRepository<Project> _projectRepo;
    private readonly IRepository<Developer> _developerRepo;
    private readonly IRepository<AiInteractionLog> _interactionRepo;
    private readonly IRepository<UserAiMemory> _memoryRepo;
    private readonly IUserConsent _consent;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AIOrchestrator> _logger;
    private readonly HttpClient _httpClient;
    private readonly IAIModelMetrics _metrics;
    private readonly IAdvisoryGuardrail _guardrail;
    private readonly ITimeSeriesAnalyzer _analyzer;
    private readonly IVectorStore _vectorStore;

    private static readonly Dictionary<string, List<ChatMessage>> SessionMemory = new();

    private const string SystemPrompt = """
        You are a Dubai Land Department (DLD) Real Estate Data Agent. Your role is to help users explore
        and understand real estate market data in Dubai.

        CRITICAL CONSTRAINTS:
        - You are STRICTLY PROHIBITED from providing personalised investment advice
        - You must NOT recommend specific properties or developers for purchase
        - You must NOT make price forecasts presented as facts
        - You must NOT provide any form of financial or legal advisory
        - Your role is EXCLUSIVELY to retrieve, present, and analyse DLD data
        - Always cite the source data and time period for every data point
        - If data is unavailable, acknowledge it honestly - never fabricate answers
        - Guide users to official DLD services when appropriate

        When providing data analysis:
        - Label all trend analysis as historical, not predictive
        - Explain methodology in plain language
        - Include appropriate disclaimers
        """;

    public AIOrchestrator(
        IRepository<Transaction> transactionRepo,
        IRepository<Zone> zoneRepo,
        IRepository<Project> projectRepo,
        IRepository<Developer> developerRepo,
        IRepository<AiInteractionLog> interactionRepo,
        IRepository<UserAiMemory> memoryRepo,
        IUserConsent consent,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<AIOrchestrator> logger,
        IHttpClientFactory httpClientFactory,
        IAIModelMetrics metrics,
        IAdvisoryGuardrail guardrail,
        ITimeSeriesAnalyzer analyzer,
        IVectorStore vectorStore)
    {
        _transactionRepo = transactionRepo;
        _zoneRepo = zoneRepo;
        _projectRepo = projectRepo;
        _developerRepo = developerRepo;
        _interactionRepo = interactionRepo;
        _memoryRepo = memoryRepo;
        _consent = consent;
        _unitOfWork = unitOfWork;
        _configuration = configuration;
        _logger = logger;
        _httpClient = httpClientFactory.CreateClient("AIService");
        _metrics = metrics;
        _guardrail = guardrail;
        _analyzer = analyzer;
        _vectorStore = vectorStore;
    }

    public async Task<AIResponse> ProcessQueryAsync(string query, string language, string? sessionId, string? userId = null)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        AIResponse? response = null;
        string? error = null;

        try
        {
            // Auto-detect Arabic script so the agent replies in the user's
            // language even when the caller didn't pass language=ar.
            if (!string.Equals(language, "ar", StringComparison.OrdinalIgnoreCase)
                && query.Any(ch => ch >= 0x0600 && ch <= 0x06FF))
            {
                language = "ar";
            }

            // RFP AI-006: cross-session memory. When an authenticated user has
            // opted in, prepend their top zone and topic preferences to the
            // RAG context so the model personalises future answers.
            var memoryEnabled = await _consent.HasAiMemoryConsentAsync(userId);
            var personalContext = memoryEnabled
                ? await BuildPersonalContextAsync(userId!)
                : string.Empty;

            // Build RAG context from DLD data
            var ragContext = await BuildRagContextAsync(query);
            if (!string.IsNullOrEmpty(personalContext))
            {
                ragContext = personalContext + "\n\n" + ragContext;
            }

            // Build conversation with session memory
            var messages = BuildConversation(query, language, ragContext, sessionId);

            // Multi-model routing (RFP 5.3): pick a tier order based on the
            // task topic. Service-navigation queries try the dedicated
            // navigation tier first when configured (cheaper / faster), then
            // fall through to the primary research model. Everything else
            // uses primary → secondary.
            var topic = ClassifyTopic(query);
            var tierOrder = TierOrderFor(topic);

            foreach (var tier in tierOrder)
            {
                response = await CallAIModelAsync(messages, tier);
                if (response != null) break;
                _logger.LogWarning("AI tier {Tier} failed, attempting next tier", tier);
            }

            response ??= new AIResponse
            {
                Answer = language == "ar"
                    ? "عذراً، لا أستطيع معالجة طلبك حالياً. يرجى المحاولة مرة أخرى لاحقاً."
                    : "I'm sorry, I'm unable to process your request at the moment. Please try again later.",
                SourceCitation = "System",
                ModelUsed = "fallback"
            };

            // Apply the no-investment-advice guardrail (RFP 5.1). If the
            // model output looks like advisory content, replace it with the
            // canned refusal — the original answer is logged so reviewers
            // can audit the trigger pattern, but never reaches the user.
            var violationReason = _guardrail.Validate(response.Answer);
            if (violationReason is not null)
            {
                _logger.LogWarning(
                    "AI response blocked by advisory guardrail ({Reason}); model={Model}",
                    violationReason, response.ModelUsed);
                response = new AIResponse
                {
                    Answer = language == "ar"
                        ? "لا يمكنني تقديم نصيحة استثمارية شخصية. يمكنني مساعدتك في استكشاف بيانات السوق الرسمية لدى دائرة الأراضي والأملاك."
                        : "I'm not able to provide personalised investment advice. I can help you explore official Dubai Land Department market data instead.",
                    SourceCitation = $"Guardrail (blocked: {violationReason})",
                    ModelUsed = response.ModelUsed
                };
            }

            // Store in session memory
            if (!string.IsNullOrEmpty(sessionId))
            {
                StoreInSession(sessionId, query, response.Answer);
            }

            // RFP AI-006: persist topic + zone mentions for opted-in users so
            // future sessions can personalise the RAG context. Failure is
            // non-fatal — memory never blocks an answer.
            if (memoryEnabled)
            {
                try { await UpdateUserMemoryAsync(userId!, query); }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to update user AI memory for {UserId}", userId);
                }
            }

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing AI query");
            error = ex.Message;
            return new AIResponse
            {
                Answer = language == "ar"
                    ? "حدث خطأ أثناء معالجة طلبك. يرجى المحاولة مرة أخرى."
                    : "An error occurred while processing your request. Please try again.",
                SourceCitation = "System",
                ModelUsed = "error"
            };
        }
        finally
        {
            sw.Stop();
            await PersistInteractionAsync(query, language, sessionId, response, error, sw.ElapsedMilliseconds);
        }
    }

    /// <summary>
    /// Write an <see cref="AiInteractionLog"/> row for every query — success
    /// or failure. Non-fatal: any persistence error is logged but never
    /// surfaced to the caller because it must not affect the answer.
    /// </summary>
    private async Task PersistInteractionAsync(
        string query, string language, string? sessionId,
        AIResponse? response, string? errorMessage, long latencyMs)
    {
        try
        {
            var answerText = response?.Answer ?? string.Empty;
            var wasRefusal = answerText.Contains("cannot provide", StringComparison.OrdinalIgnoreCase)
                             || answerText.Contains("لا يمكن", StringComparison.Ordinal);

            await _interactionRepo.AddAsync(new AiInteractionLog
            {
                SessionId = sessionId,
                Language = language,
                Query = Truncate(query, 2000),
                Topic = ClassifyTopic(query),
                Answer = Truncate(answerText, 8000),
                SourceCitation = response?.SourceCitation,
                ModelUsed = response?.ModelUsed,
                WasRefusal = wasRefusal,
                LatencyMs = (int)Math.Min(latencyMs, int.MaxValue),
                Success = errorMessage is null,
                ErrorMessage = errorMessage is null ? null : Truncate(errorMessage, 1000)
            });
            await _unitOfWork.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist AI interaction log — continuing without audit row.");
        }
    }

    private static string ClassifyTopic(string query)
    {
        var q = query.ToLowerInvariant();
        if (q.Contains("register") || q.Contains("rera") || q.Contains("licence") || q.Contains("license")
            || q.Contains("ejari") || q.Contains("valuation") || q.Contains("service"))
            return "dld-service-nav";
        if (q.Contains("developer")) return "developer";
        if (q.Contains("rent") || q.Contains("yield")) return "rental";
        if (q.Contains("price") || q.Contains("sqft") || q.Contains("psf")) return "price";
        if (q.Contains("project") || q.Contains("off-plan") || q.Contains("off plan")) return "project";
        if (q.Contains("transaction")) return "transaction";
        if (q.Contains("esg") || q.Contains("estidama") || q.Contains("leed")) return "esg";
        return "general";
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) ? string.Empty : value.Length <= max ? value : value[..max];

    private async Task<string> BuildRagContextAsync(string query)
    {
        var context = new StringBuilder();
        var queryLower = query.ToLowerInvariant();

        // Semantic retrieval (RAG): pull the most relevant indexed records from
        // the vector store first, so the model is grounded on the closest data
        // even when the query wording doesn't match the keyword branches below.
        try
        {
            await _vectorStore.EnsureIndexedAsync();
            var hits = _vectorStore.Search(query, topK: 12);
            if (hits.Count > 0)
            {
                context.AppendLine($"Most relevant records (semantic vector search — top {hits.Count} of {_vectorStore.DocumentCount} indexed):");
                foreach (var (doc, _) in hits)
                    context.AppendLine($"- [{doc.EntityType}] {doc.Content}");
                context.AppendLine();
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Vector retrieval failed; using deterministic context only.");
        }

        // Retrieve relevant data based on query keywords
        if (ContainsAny(queryLower, "transaction", "sale", "price", "volume", "market"))
        {
            var recentTransactions = await _transactionRepo.Query()
                .Include(t => t.Zone)
                .OrderByDescending(t => t.TransactionDate)
                .Take(100)
                .ToListAsync();

            var totalTransactions = await _transactionRepo.Query().CountAsync();

            var stats = recentTransactions.GroupBy(t => t.Zone.Name)
                .Select(g => new
                {
                    Zone = g.Key,
                    Count = g.Count(),
                    AvgPrice = g.Average(t => t.PricePerSqft),
                    TotalValue = g.Sum(t => t.TransactionValue)
                }).ToList();

            context.AppendLine($"Transaction totals: {totalTransactions:N0} transactions recorded in total across all zones. The per-zone figures below come from the {recentTransactions.Count} most recent transactions (a sample of the full set, not the total):");
            foreach (var s in stats)
            {
                context.AppendLine($"- {s.Zone}: {s.Count} transactions, Avg AED {s.AvgPrice:N0}/sqft, Total AED {s.TotalValue:N0}");
            }

            // Deterministic stats (RFP AI004) — when the query asks about
            // trends or anomalies, supply the deterministic figures so the
            // model quotes them directly instead of hallucinating math.
            if (ContainsAny(queryLower, "trend", "anomaly", "anomalies", "spike", "drop", "decline", "growth"))
            {
                var monthlySeries = await _transactionRepo.Query()
                    .GroupBy(t => new { t.TransactionDate.Year, t.TransactionDate.Month })
                    .Select(g => new
                    {
                        g.Key.Year,
                        g.Key.Month,
                        Count = g.Count()
                    })
                    .OrderBy(g => g.Year).ThenBy(g => g.Month)
                    .ToListAsync();

                var series = monthlySeries
                    .Select(m => new TimeSeriesPoint(new DateTime(m.Year, m.Month, 1), m.Count))
                    .ToList();

                if (series.Count >= 4)
                {
                    var trend = _analyzer.AnalyzeTrend(series);
                    var anomalies = _analyzer.DetectAnomalies(series);
                    context.AppendLine();
                    context.AppendLine("Deterministic Time-Series Analysis (historical, not predictive):");
                    context.AppendLine($"- Trend: {trend.Direction} (slope {trend.Slope:N2} txns/month, R² {trend.RSquared:N3})");
                    if (anomalies.Count > 0)
                    {
                        context.AppendLine($"- Anomalies (|z| ≥ 2.5): {anomalies.Count} detected");
                        foreach (var a in anomalies.Take(5))
                        {
                            context.AppendLine($"  · {a.Period:yyyy-MM}: {a.Value:N0} txns ({a.Direction}, z={a.ZScore:N2})");
                        }
                    }
                    else
                    {
                        context.AppendLine("- Anomalies: none above the 2.5σ threshold");
                    }
                }
            }
        }

        if (ContainsAny(queryLower, "zone", "area", "community", "location"))
        {
            var zones = await _zoneRepo.Query().ToListAsync();
            context.AppendLine($"Dubai Zones ({zones.Count} total):");
            foreach (var z in zones.Take(20))
            {
                context.AppendLine($"- {z.Name} ({z.NameAr})");
            }
        }

        if (ContainsAny(queryLower, "project", "developer", "construction", "off-plan", "completed"))
        {
            var projects = await _projectRepo.Query()
                .Include(p => p.Developer)
                .Include(p => p.Zone)
                .ToListAsync();

            var byStatus = projects.GroupBy(p => p.Status)
                .Select(g => $"{g.Key}: {g.Count()} projects").ToList();

            context.AppendLine("Project Summary:");
            foreach (var s in byStatus)
            {
                context.AppendLine($"- {s}");
            }
        }

        if (ContainsAny(queryLower, "developer", "rating", "score"))
        {
            var developers = await _developerRepo.Query()
                .Include(d => d.Projects)
                .Where(d => d.IsActive)
                .ToListAsync();

            context.AppendLine("Active Developers:");
            foreach (var d in developers)
            {
                context.AppendLine($"- {d.Name}: {d.Projects.Count} projects");
            }
        }

        return context.Length > 0 ? context.ToString() : "No specific DLD data found matching the query context.";
    }

    private List<ChatMessage> BuildConversation(string query, string language, string ragContext, string? sessionId)
    {
        var messages = new List<ChatMessage>
        {
            new() { Role = "system", Content = SystemPrompt },
            new()
            {
                Role = "system",
                Content = $"DLD Database Context:\n{ragContext}\n\nRespond in {(language == "ar" ? "Arabic" : "English")}."
            }
        };

        // Add session history
        if (!string.IsNullOrEmpty(sessionId) && SessionMemory.TryGetValue(sessionId, out var history))
        {
            messages.AddRange(history.TakeLast(10)); // Last 5 turns
        }

        messages.Add(new ChatMessage { Role = "user", Content = query });
        return messages;
    }

    private async Task<AIResponse?> CallAIModelAsync(List<ChatMessage> messages, string modelTier)
    {
        var apiKey = _configuration[$"AI:{modelTier}:ApiKey"];
        var endpoint = _configuration[$"AI:{modelTier}:Endpoint"];
        var modelName = _configuration[$"AI:{modelTier}:ModelName"] ?? "gpt-4o";
        var region = _configuration[$"AI:{modelTier}:Region"] ?? "uae-north";
        var apiVersion = _configuration[$"AI:{modelTier}:ApiVersion"];

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
        {
            _logger.LogWarning("AI {ModelTier} configuration missing", modelTier);
            _metrics.SetMetadata(modelName, modelTier, region);
            _metrics.SetActive(modelName, false);
            return null;
        }

        _metrics.SetMetadata(modelName, modelTier, region);
        _metrics.SetActive(modelName, true);
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        try
        {
            var request = new
            {
                model = modelName,
                messages = messages.Select(m => new { role = m.Role, content = m.Content }),
                temperature = 0.3,
                max_tokens = 2000
            };

            // Azure OpenAI authenticates with an "api-key" header against a
            // deployment-scoped URL; native OpenAI-compatible gateways use a
            // Bearer token against the endpoint as-is. Detect Azure either by an
            // explicit ApiVersion (Azure-only) or an *.azure.com host, and shape
            // the request accordingly. Both return the same choices[].message
            // body, so the parsing below is unchanged.
            var isAzure = !string.IsNullOrEmpty(apiVersion)
                || endpoint.Contains("azure.com", StringComparison.OrdinalIgnoreCase);

            var requestUri = endpoint;
            if (isAzure && endpoint.IndexOf("/chat/completions", StringComparison.OrdinalIgnoreCase) < 0)
            {
                var version = string.IsNullOrEmpty(apiVersion) ? "2024-08-01-preview" : apiVersion;
                requestUri = $"{endpoint.TrimEnd('/')}/openai/deployments/{modelName}/chat/completions?api-version={version}";
            }

            var httpRequest = new HttpRequestMessage(HttpMethod.Post, requestUri)
            {
                Content = new StringContent(JsonSerializer.Serialize(request), Encoding.UTF8, "application/json")
            };
            if (isAzure)
                httpRequest.Headers.Add("api-key", apiKey);
            else
                httpRequest.Headers.Add("Authorization", $"Bearer {apiKey}");

            var httpResponse = await _httpClient.SendAsync(httpRequest);
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("AI API returned {StatusCode}", httpResponse.StatusCode);
                _metrics.RecordFailure(modelName, $"HTTP {(int)httpResponse.StatusCode}");
                return null;
            }

            var responseContent = await httpResponse.Content.ReadAsStringAsync();
            var responseJson = JsonDocument.Parse(responseContent);
            var answer = responseJson.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString() ?? "";

            // Check if the response contains chart data (JSON block)
            string? chartConfig = null;
            string? dataJson = null;
            if (answer.Contains("```chart"))
            {
                var chartStart = answer.IndexOf("```chart") + 8;
                var chartEnd = answer.IndexOf("```", chartStart);
                if (chartEnd > chartStart)
                {
                    chartConfig = answer.Substring(chartStart, chartEnd - chartStart).Trim();
                }
            }

            stopwatch.Stop();
            _metrics.RecordSuccess(modelName, stopwatch.Elapsed.TotalMilliseconds);

            return new AIResponse
            {
                Answer = answer,
                ChartConfigJson = chartConfig,
                DataJson = dataJson,
                SourceCitation = "Dubai Land Department Official Records",
                ModelUsed = modelName
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error calling AI model {ModelTier}", modelTier);
            _metrics.RecordFailure(modelName, ex.Message);
            return null;
        }
    }

    private void StoreInSession(string sessionId, string query, string answer)
    {
        if (!SessionMemory.ContainsKey(sessionId))
            SessionMemory[sessionId] = [];

        SessionMemory[sessionId].Add(new ChatMessage { Role = "user", Content = query });
        SessionMemory[sessionId].Add(new ChatMessage { Role = "assistant", Content = answer });

        // Limit session size
        if (SessionMemory[sessionId].Count > 20)
            SessionMemory[sessionId] = SessionMemory[sessionId].TakeLast(20).ToList();
    }

    // ------------------------------------------------------------------
    // RFP AI-006 — cross-session memory (zone preferences + frequent topics)
    // ------------------------------------------------------------------

    /// <summary>
    /// Reads the opted-in user's top 5 zone preferences and top 3 topics from
    /// <see cref="UserAiMemory"/> and renders them as a short RAG preamble.
    /// </summary>
    private async Task<string> BuildPersonalContextAsync(string userId)
    {
        var rows = await _memoryRepo.Query()
            .Where(m => m.UserId == userId)
            .ToListAsync();
        if (rows.Count == 0) return string.Empty;

        var zones = rows.Where(r => r.Kind == "zone")
            .OrderByDescending(r => r.Frequency).ThenByDescending(r => r.LastUsedAt)
            .Take(5).Select(r => r.Key).ToList();
        var topics = rows.Where(r => r.Kind == "topic")
            .OrderByDescending(r => r.Frequency).ThenByDescending(r => r.LastUsedAt)
            .Take(3).Select(r => r.Key).ToList();

        var sb = new StringBuilder();
        sb.AppendLine("User context (cross-session memory, opted in):");
        if (zones.Count > 0) sb.AppendLine($"- Zones of interest: {string.Join(", ", zones)}");
        if (topics.Count > 0) sb.AppendLine($"- Frequent topics: {string.Join(", ", topics)}");
        return sb.ToString();
    }

    /// <summary>
    /// Upserts zone and topic mentions into <see cref="UserAiMemory"/>.
    /// Matches zones by case-insensitive substring against the persisted zone
    /// names; the classified topic is always stored.
    /// </summary>
    private async Task UpdateUserMemoryAsync(string userId, string query)
    {
        var now = DateTime.UtcNow;
        var queryLower = query.ToLowerInvariant();

        // Resolve detected zone names. Do this once and cache for the method —
        // zones are a small fixed set (~15), so a full scan is cheap.
        var zones = await _zoneRepo.Query().ToListAsync();
        var mentionedZones = zones
            .Where(z => queryLower.Contains(z.Name.ToLowerInvariant()))
            .Select(z => z.Name)
            .Distinct()
            .ToList();

        var topic = ClassifyTopic(query);
        var candidates = mentionedZones.Select(z => (Kind: "zone", Key: z)).ToList();
        candidates.Add(("topic", topic));

        if (candidates.Count == 0) return;

        var existing = await _memoryRepo.Query()
            .Where(m => m.UserId == userId)
            .ToListAsync();

        var added = false;
        foreach (var (kind, key) in candidates)
        {
            var hit = existing.FirstOrDefault(m => m.Kind == kind && m.Key == key);
            if (hit is not null)
            {
                hit.Frequency += 1;
                hit.LastUsedAt = now;
            }
            else
            {
                await _memoryRepo.AddAsync(new UserAiMemory
                {
                    UserId = userId,
                    Kind = kind,
                    Key = key,
                    Frequency = 1,
                    LastUsedAt = now
                });
                added = true;
            }
        }

        if (added || existing.Count > 0)
        {
            await _unitOfWork.SaveChangesAsync();
        }
    }

    private static bool ContainsAny(string text, params string[] keywords)
        => keywords.Any(k => text.Contains(k, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Maps a classified query topic to the tier evaluation order. The tiers
    /// are config keys read by <see cref="CallAIModelAsync"/>:
    /// AI:primary:*, AI:secondary:*, AI:nav:*, AI:analytics:*. Tiers that
    /// are missing config simply degrade gracefully (returns null and the
    /// next tier in the order is tried).
    /// </summary>
    private static IReadOnlyList<string> TierOrderFor(string topic) => topic switch
    {
        // Service-navigation queries are short and templated — the cheaper
        // nav tier handles them first when configured, with primary as the
        // fallback for ambiguous cases.
        "dld-service-nav" => new[] { "nav", "primary", "secondary" },

        // Deep analytics queries (anomaly, correlation, trend) prefer a
        // dedicated analytics tier that may be tuned with longer context
        // windows.
        "transaction" or "price" or "rental" or "developer" or "project"
            => new[] { "analytics", "primary", "secondary" },

        // Everything else uses the standard primary → secondary fallback.
        _ => new[] { "primary", "secondary" }
    };
}

public class ChatMessage
{
    public string Role { get; set; } = default!;
    public string Content { get; set; } = default!;
}
