using System.Text.Json;
using IRETP.Web.Services;

namespace IRETP.Web.Services;

/// <summary>
/// Backing service for the /ai-agent conversational page. Attempts to call the
/// WebAPI endpoint /api/ai/query; if unreachable, falls back to a deterministic
/// local simulator that demonstrates the RFP-required behaviour: grounded
/// answers with DLD source citation, chart-capable replies, and a refusal path
/// for investment-advice queries (Section 5.1 CRITICAL CONSTRAINT).
/// </summary>
public class AiChatService
{
    public sealed record ChatMessage(
        string Role,          // user | agent
        string Text,
        string? ChartConfigJson,
        string? Citation,
        string? ModelUsed,
        DateTime At,
        IReadOnlyList<string>? FollowUps = null);

    public sealed class Session
    {
        public string Id { get; init; } = Guid.NewGuid().ToString("n");
        public List<ChatMessage> Messages { get; } = new();
    }

    private readonly WebApiClient _api;
    private readonly MarketDataService _market;

    public AiChatService(WebApiClient api, MarketDataService market)
    {
        _api = api;
        _market = market;
    }

    public async Task<ChatMessage> AskAsync(Session session, string query, string language)
    {
        session.Messages.Add(new ChatMessage("user", query, null, null, null, DateTime.UtcNow));

        // Guardrail — never provide investment advice (RFP 5.1).
        if (LooksLikeInvestmentAdvice(query))
        {
            var refusal = language == "ar"
                ? "لا يمكن للمساعد تقديم نصائح استثمارية أو توصيات بشراء محدد. يمكنني مشاركة بيانات السوق الواقعية من سجل DLD بدلاً من ذلك."
                : "The IRETP Agent cannot provide personalised investment advice or buy recommendations. I can share factual DLD market data on any area, project, or developer you'd like.";
            var reply = new ChatMessage("agent", refusal, null,
                language == "ar" ? "ضوابط الامتثال في IRETP" : "IRETP compliance guardrail",
                "policy-guard", DateTime.UtcNow,
                SuggestFollowUps("guardrail", language));
            session.Messages.Add(reply);
            return reply;
        }

        // Try the real backend first (WebAPI).
        try
        {
            var response = await _api.QueryAiAgentAsync(query, language, session.Id);
            if (response != null)
            {
                var reply = new ChatMessage("agent", response.Answer,
                    response.ChartConfigJson, response.SourceCitation, response.ModelUsed, DateTime.UtcNow,
                    SuggestFollowUps(InferTopic(query), language));
                session.Messages.Add(reply);
                return reply;
            }
        }
        catch
        {
            // swallow — fall through to local simulator
        }

        // Local deterministic simulator (fixture mode).
        var simulated = SimulateAnswer(query, language);
        session.Messages.Add(simulated);
        return simulated;
    }

    /// <summary>
    /// Streaming variant of <see cref="AskAsync"/>. Resolves the reply, then
    /// yields progressive token chunks so the UI can render the answer as it
    /// arrives — improves perceived latency against the RFP AI-002
    /// &lt;30 s chart generation target. The final chunk carries the full
    /// ChatMessage; the message is only added to the session at that point
    /// so the UI can mask the user's view of the last agent bubble with the
    /// progressive text until streaming completes.
    /// </summary>
    public async IAsyncEnumerable<AiStreamChunk> AskStreamAsync(
        Session session, string query, string language,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Push the user question so it shows up immediately.
        session.Messages.Add(new ChatMessage("user", query, null, null, null, DateTime.UtcNow));

        ChatMessage reply;

        if (LooksLikeInvestmentAdvice(query))
        {
            var refusal = language == "ar"
                ? "لا يمكن للمساعد تقديم نصائح استثمارية أو توصيات بشراء محدد. يمكنني مشاركة بيانات السوق الواقعية من سجل DLD بدلاً من ذلك."
                : "The IRETP Agent cannot provide personalised investment advice or buy recommendations. I can share factual DLD market data on any area, project, or developer you'd like.";
            reply = new ChatMessage("agent", refusal, null,
                language == "ar" ? "ضوابط الامتثال في IRETP" : "IRETP compliance guardrail",
                "policy-guard", DateTime.UtcNow, SuggestFollowUps("guardrail", language));
        }
        else
        {
            ChatMessage? backendReply = null;
            try
            {
                var response = await _api.QueryAiAgentAsync(query, language, session.Id);
                if (response != null)
                {
                    backendReply = new ChatMessage("agent", response.Answer,
                        response.ChartConfigJson, response.SourceCitation, response.ModelUsed, DateTime.UtcNow,
                        SuggestFollowUps(InferTopic(query), language));
                }
            }
            catch { /* fall through to simulator */ }

            reply = backendReply ?? SimulateAnswer(query, language);
        }

        // Progressive token reveal — chunk on word boundaries so newlines
        // never split mid-word. 12 ms per token gives ~80 WPM, a
        // human-readable pace that keeps the overall response snappy.
        var words = reply.Text.Split(' ');
        var acc = new System.Text.StringBuilder();
        for (var i = 0; i < words.Length; i++)
        {
            if (ct.IsCancellationRequested) yield break;
            if (i > 0) acc.Append(' ');
            acc.Append(words[i]);
            try { await Task.Delay(12, ct); } catch { yield break; }
            yield return new AiStreamChunk(acc.ToString(), IsFinal: false, FinalMessage: null);
        }

        session.Messages.Add(reply);
        yield return new AiStreamChunk(reply.Text, IsFinal: true, FinalMessage: reply);
    }

    /// <summary>Single progressive chunk emitted by <see cref="AskStreamAsync"/>.</summary>
    public sealed record AiStreamChunk(string Text, bool IsFinal, ChatMessage? FinalMessage);

    private ChatMessage SimulateAnswer(string query, string language)
    {
        var q = query.ToLowerInvariant();
        string text;
        string? chartJson = null;

        if (q.Contains("top") && (q.Contains("area") || q.Contains("zone")))
        {
            var areas = _market.GetAreaRankingsAsync().Result.Take(5).ToList();
            text = language == "ar"
                ? $"أعلى ٥ مناطق من حيث قيمة المبيعات:\n" + string.Join("\n", areas.Select((a, i) => $"{i + 1}. {a.Name} — {MarketDataService.FormatAed(a.TotalValue)}"))
                : $"Top 5 areas by sales value:\n" + string.Join("\n", areas.Select((a, i) => $"{i + 1}. {a.Name} — {MarketDataService.FormatAed(a.TotalValue)}"));
            chartJson = JsonSerializer.Serialize(new
            {
                type = "bar",
                data = new
                {
                    labels = areas.Select(a => a.Name).ToArray(),
                    datasets = new[] { new { label = "Sales AED", data = areas.Select(a => (double)a.TotalValue).ToArray() } }
                }
            });
        }
        else if (q.Contains("developer"))
        {
            var devs = _market.GetDeveloperRankingsAsync().Result.Take(5).ToList();
            text = language == "ar"
                ? $"أعلى ٥ مطورين حسب القيمة:\n" + string.Join("\n", devs.Select((d, i) => $"{i + 1}. {d.Name} — {MarketDataService.FormatAed(d.TotalValue)}"))
                : $"Top 5 developers by value:\n" + string.Join("\n", devs.Select((d, i) => $"{i + 1}. {d.Name} — {MarketDataService.FormatAed(d.TotalValue)}"));
            chartJson = JsonSerializer.Serialize(new
            {
                type = "bar",
                data = new
                {
                    labels = devs.Select(d => d.Name).ToArray(),
                    datasets = new[] { new { label = "Share %", data = devs.Select(d => d.MarketSharePercent).ToArray() } }
                }
            });
        }
        else if (q.Contains("price") || q.Contains("psf") || q.Contains("sqft"))
        {
            var rows = _market.GetPriceIndexAsync().Result.Take(8).ToList();
            text = language == "ar"
                ? "متوسط السعر للقدم المربع حسب المنطقة (أعلى ٨):\n" + string.Join("\n", rows.Select(r => $"• {r.AreaName}: AED {r.AvgPricePerSqft:N0}/sqft"))
                : "Average price per sqft by area (top 8):\n" + string.Join("\n", rows.Select(r => $"• {r.AreaName}: AED {r.AvgPricePerSqft:N0}/sqft"));
            chartJson = JsonSerializer.Serialize(new
            {
                type = "bar",
                data = new
                {
                    labels = rows.Select(r => r.AreaName).ToArray(),
                    datasets = new[] { new { label = "AED/sqft", data = rows.Select(r => (double)r.AvgPricePerSqft).ToArray() } }
                }
            });
        }
        else if (q.Contains("rental") || q.Contains("yield"))
        {
            text = language == "ar"
                ? "متوسط العائد الإيجاري في دبي حاليًا حوالي ٦٫٨٪ (سكني). أعلى العائدات في قرية جميرا الدائرية ودبي الجنوب ودبي للاستثمار. المصدر: معاملات الإيجار المسجلة لدى نظام إيجاري."
                : "Average gross rental yield across Dubai residential is ~6.8%. Highest-yielding zones: JVC, Dubai South, Dubai Investment Park. Source: Ejari-registered rentals.";
        }
        else
        {
            text = language == "ar"
                ? "يمكنني المساعدة في أسئلة تتعلق بمعاملات DLD، المناطق، المطورين، الأسعار، والعوائد الإيجارية. ما الذي تود معرفته؟"
                : "I can help with questions about DLD transactions, zones, developers, prices, and rental yields. What would you like to know?";
        }

        return new ChatMessage("agent", text, chartJson,
            "DLD transaction registry + Ejari (fixture mode)",
            "local-simulator",
            DateTime.UtcNow);
    }

    private static bool LooksLikeInvestmentAdvice(string q)
    {
        var lower = q.ToLowerInvariant();
        string[] triggers =
        {
            "should i buy", "should i invest", "is it a good investment",
            "recommend a property", "recommend a project", "where should i buy",
            "will the price go up", "price forecast", "predict the price",
            "future price", "best investment", "guaranteed return"
        };
        return triggers.Any(t => lower.Contains(t));
    }

    private static string InferTopic(string query)
    {
        var q = query.ToLowerInvariant();
        if (q.Contains("yield") || q.Contains("rental") || q.Contains("rent")) return "yield";
        if (q.Contains("price") || q.Contains("psf") || q.Contains("sqft")) return "price";
        if (q.Contains("developer") || q.Contains("emaar") || q.Contains("damac")) return "developer";
        if (q.Contains("project") || q.Contains("off-plan") || q.Contains("off plan")) return "project";
        if (q.Contains("transaction")) return "transaction";
        return "market";
    }

    private static IReadOnlyList<string> SuggestFollowUps(string topic, string language) =>
        (topic, language) switch
        {
            ("guardrail", "ar") => new[]
            {
                "ما هو متوسط السعر للقدم المربع في دبي مارينا؟",
                "أيّ المناطق سجّلت أعلى حجم معاملات الشهر الماضي؟",
                "ما هو متوسط العائد الإيجاري في قرية جميرا الدائرية؟"
            },
            ("guardrail", _) => new[]
            {
                "What is the average price per sqft in Dubai Marina?",
                "Which zones had the highest transaction volume last month?",
                "What is the current rental yield in JVC?"
            },
            ("yield", "ar") => new[]
            {
                "أيّ المناطق تسجّل أعلى عائد إيجاري؟",
                "اعرض اتجاه العائد الإيجاري خلال ٥ سنوات.",
                "قارن العائد الإيجاري بين الشقق والفلل."
            },
            ("yield", _) => new[]
            {
                "Which zones have the highest rental yield?",
                "Show the 5-year rental yield trend.",
                "Compare apartment vs. villa yields."
            },
            ("price", "ar") => new[]
            {
                "اعرض متوسط السعر للقدم المربع لكل منطقة.",
                "كيف تغيّر السعر على أساس سنوي؟",
                "قارن الأسعار في دبي مارينا وداون تاون."
            },
            ("price", _) => new[]
            {
                "Show average price per sqft by zone.",
                "How has price changed year-over-year?",
                "Compare prices in Dubai Marina vs. Downtown."
            },
            ("developer", "ar") => new[]
            {
                "أعلى ٥ مطورين حسب القيمة.",
                "ما هي نسبة التسليم في الموعد للمطورين؟",
                "اعرض المشاريع النشطة للمطور المختار."
            },
            ("developer", _) => new[]
            {
                "Show top 5 developers by value.",
                "What is the on-time delivery rate per developer?",
                "Show active projects for the selected developer."
            },
            ("project", "ar") => new[]
            {
                "اعرض المشاريع قيد الإنشاء في الخريطة.",
                "أيّ المشاريع يُتوقع تسليمها هذا العام؟",
                "ما هي نسبة البيع في المشروع المختار؟"
            },
            ("project", _) => new[]
            {
                "Show under-construction projects on the map.",
                "Which projects are expected to deliver this year?",
                "What's the sales completion rate on this project?"
            },
            _ => new[]
            {
                "Show top zones by transaction volume.",
                "What is the overall market trend this year?",
                "Compare off-plan vs. ready-property activity."
            }
        };
}
