using System.Text;
using System.Text.Json;
using IRETP.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Grounds "how does X work / why was X done this way" questions on the
/// technical documentation pack. The Compliance Matrix is chunked one row per
/// requirement; every other docs/*.md is chunked per heading section. Chunks
/// are scored by keyword overlap with the question and the top hits are sent
/// as context to the same AI:primary chat-completions deployment the AI Agent
/// uses.
/// </summary>
public class ComplianceAskService : IComplianceAsk
{
    private const string PrimaryTier = "primary";
    private const int TopChunks = 16;
    private const int MaxSectionChunkChars = 1500;
    private const string MatrixFileName = "COMPLIANCE_MATRIX.md";

    private const string SystemPrompt =
        "You are the technical assistant for the IRETP (Integrated Real Estate Transparency Platform) " +
        "reference implementation for Dubai Land Department RFP DLD-IRETP-2026-001. " +
        "You answer questions about the build: HOW a feature or mechanism works end-to-end, WHY an approach was chosen, " +
        "which components implement a requirement, and how it is verified. " +
        "Ground every claim strictly in the documentation context provided (compliance matrix rows and technical-doc sections). " +
        "Cite requirement IDs (e.g. FR-008, AI-006, §10.2) and implementation file paths from the context. " +
        "If the context does not cover the question, say so plainly — never invent files, tests, or requirement numbers. " +
        "Keep answers concise and structured; use short bullet lists where they help.";

    private static readonly HashSet<string> Stopwords = new(StringComparer.OrdinalIgnoreCase)
    {
        "how", "why", "what", "which", "does", "do", "did", "is", "are", "was", "were", "the", "a", "an",
        "of", "in", "on", "to", "for", "and", "or", "with", "we", "you", "it", "this", "that", "my", "our",
        "system", "platform", "iretp", "address", "addresses", "addressed", "addressing", "problem",
        "problems", "issue", "issues", "handle", "handles", "handled", "implement", "implements",
        "implemented", "implementation", "solution", "about", "there", "have", "has", "can", "will", "be"
    };

    private readonly IConfiguration _configuration;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ComplianceAskService> _logger;

    private static readonly object ChunkLock = new();
    private static List<Chunk>? _chunks;
    private static DateTime _chunksLoadedFromWriteUtc;

    public ComplianceAskService(
        IConfiguration configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ComplianceAskService> logger)
    {
        _configuration = configuration;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ComplianceAskResult> AskAsync(string question, CancellationToken ct = default)
    {
        var chunks = LoadChunks();
        var hits = Rank(chunks, question).Take(TopChunks).ToList();

        var context = new StringBuilder();
        context.AppendLine("Documentation excerpts (compliance-matrix rows and technical-doc sections):");
        foreach (var hit in hits)
        {
            context.Append("- [").Append(hit.Label).Append("] ").AppendLine(hit.Text);
        }

        var answer = await CallModelAsync(question, context.ToString(), ct);
        if (answer is null)
        {
            return new ComplianceAskResult
            {
                Answer = "The AI model tier is not configured on this host, so I can only point you at the raw sources below. " +
                         "See docs/COMPLIANCE_MATRIX.md and docs/ARCHITECTURE.md for the full picture.",
                ModelUsed = null,
                Sources = hits.Select(h => h.Label).Distinct().ToList()
            };
        }

        return new ComplianceAskResult
        {
            Answer = answer,
            ModelUsed = _configuration[$"AI:{PrimaryTier}:ModelName"] ?? "gpt-4o",
            Sources = hits.Select(h => h.Label).Distinct().ToList()
        };
    }

    // ------------------------------------------------------------------
    // Retrieval
    // ------------------------------------------------------------------

    private sealed record Chunk(string Label, string Text, string LowerText);

    private static IEnumerable<Chunk> Rank(List<Chunk> chunks, string question)
    {
        var words = Tokenize(question);
        return chunks
            .Select(c => (chunk: c, score: Score(c, words, question)))
            .Where(x => x.score > 0)
            .OrderByDescending(x => x.score)
            .Select(x => x.chunk);
    }

    private static double Score(Chunk chunk, List<string> words, string question)
    {
        double score = 0;
        foreach (var w in words)
        {
            if (chunk.LowerText.Contains(w, StringComparison.Ordinal)) score += 1;
        }

        // Exact requirement-id mention (fr-008, an-003, ai-006, esc-001, §10.2)
        foreach (var id in ExtractIds(question))
        {
            if (chunk.LowerText.Contains(id, StringComparison.Ordinal)) score += 5;
        }

        return score;
    }

    private static List<string> Tokenize(string text) =>
        text.ToLowerInvariant()
            .Split([' ', '\t', ',', '.', '?', '!', ';', ':', '(', ')', '"', '\''], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length > 2 && !Stopwords.Contains(w))
            .Distinct()
            .ToList();

    private static IEnumerable<string> ExtractIds(string question)
    {
        foreach (System.Text.RegularExpressions.Match m in System.Text.RegularExpressions.Regex.Matches(
                     question, @"\b(fr|an|ai|esc)[-\s]?(\d{1,3})\b|§\s?(\d+(\.\d+)*)",
                     System.Text.RegularExpressions.RegexOptions.IgnoreCase))
        {
            var v = m.Value.ToLowerInvariant().Replace(" ", "");
            // normalise "fr8" / "fr-8" → "fr-008"
            var im = System.Text.RegularExpressions.Regex.Match(v, @"^(fr|an|ai|esc)-?(\d+)$");
            yield return im.Success ? $"{im.Groups[1].Value}-{int.Parse(im.Groups[2].Value):000}" : v;
        }
    }

    private List<Chunk> LoadChunks()
    {
        var docsDir = FindDocsDir();
        var files = docsDir is not null
            ? Directory.GetFiles(docsDir, "*.md", SearchOption.TopDirectoryOnly).OrderBy(f => f).ToArray()
            : [];
        var writeUtc = files.Length > 0 ? files.Max(File.GetLastWriteTimeUtc) : DateTime.MinValue;

        lock (ChunkLock)
        {
            if (_chunks is not null && writeUtc == _chunksLoadedFromWriteUtc) return _chunks;

            var chunks = new List<Chunk>();
            if (files.Length == 0)
            {
                _logger.LogWarning("docs/*.md not found — compliance-ask runs without grounding");
                _chunks = chunks;
                return chunks;
            }

            foreach (var file in files)
            {
                if (Path.GetFileName(file).Equals(MatrixFileName, StringComparison.OrdinalIgnoreCase))
                    ChunkMatrixRows(file, chunks);
                else
                    ChunkDocSections(file, chunks);
            }

            _chunks = chunks;
            _chunksLoadedFromWriteUtc = writeUtc;
            _logger.LogInformation("Docs indexed for ask: {Count} chunks from {FileCount} files in {Dir}",
                chunks.Count, files.Length, docsDir);
            return chunks;
        }
    }

    /// <summary>Compliance matrix: one chunk per requirement table row.</summary>
    private static void ChunkMatrixRows(string path, List<Chunk> chunks)
    {
        string section = "", subsection = "";
        string[] header = [];
        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.Trim();
            if (line.StartsWith("## ") && !line.StartsWith("###"))
            {
                section = line[3..].Trim();
                subsection = "";
                header = [];
                continue;
            }
            if (line.StartsWith("### "))
            {
                subsection = line[4..].Trim();
                header = [];
                continue;
            }
            if (line.StartsWith('|'))
            {
                var cells = line.Trim('|').Split('|').Select(c => c.Replace("\\|", "|").Trim()).ToArray();
                if (cells.All(c => c.Length == 0 || c.All(ch => ch is '-' or ':' or ' '))) continue;
                if (header.Length == 0) { header = cells; continue; }

                var text = string.Join("; ", header.Zip(cells, (h, c) => $"{h}: {c}").Where(s => !s.EndsWith(": ")));
                var label = ComposeLabel(section, subsection, cells[0]);
                chunks.Add(new Chunk(label, text, text.ToLowerInvariant()));
                continue;
            }
            if (line.Length > 0 && !line.StartsWith('#') && line != "---")
            {
                var label = ComposeLabel(section, subsection, null);
                chunks.Add(new Chunk(label, line, line.ToLowerInvariant()));
            }
        }
    }

    /// <summary>
    /// Other technical docs: one chunk per heading section (split when a
    /// section exceeds <see cref="MaxSectionChunkChars"/> so a single giant
    /// section can't crowd out the rest of the context window).
    /// </summary>
    private static void ChunkDocSections(string path, List<Chunk> chunks)
    {
        var docName = Path.GetFileNameWithoutExtension(path).Replace('_', ' ');
        string section = "";
        var body = new StringBuilder();

        void Flush()
        {
            var text = body.ToString().Trim();
            body.Clear();
            if (text.Length == 0) return;
            var label = section.Length > 0 ? $"{docName} › {section}" : docName;
            for (var start = 0; start < text.Length; start += MaxSectionChunkChars)
            {
                var piece = text.Substring(start, Math.Min(MaxSectionChunkChars, text.Length - start));
                chunks.Add(new Chunk(label, piece, piece.ToLowerInvariant()));
            }
        }

        foreach (var raw in File.ReadLines(path))
        {
            var line = raw.TrimEnd();
            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                Flush();
                section = trimmed.TrimStart('#').Trim();
                continue;
            }
            if (trimmed == "---") continue;
            if (trimmed.Length > 0) body.Append(trimmed).Append(' ');
        }
        Flush();
    }

    private static string ComposeLabel(string section, string subsection, string? rowKey)
    {
        var parts = new List<string>(3);
        if (section.Length > 0) parts.Add(section);
        if (subsection.Length > 0) parts.Add(subsection);
        if (!string.IsNullOrWhiteSpace(rowKey)) parts.Add(rowKey!);
        return string.Join(" › ", parts);
    }

    private static string? FindDocsDir()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "IRETP.sln")))
            {
                var candidate = Path.Combine(dir.FullName, "docs");
                return Directory.Exists(candidate) ? candidate : null;
            }
            dir = dir.Parent;
        }
        return null;
    }

    // ------------------------------------------------------------------
    // Model call — same config keys + Azure/OpenAI shape as AIOrchestrator
    // ------------------------------------------------------------------

    private async Task<string?> CallModelAsync(string question, string context, CancellationToken ct)
    {
        var apiKey = _configuration[$"AI:{PrimaryTier}:ApiKey"];
        var endpoint = _configuration[$"AI:{PrimaryTier}:Endpoint"];
        var modelName = _configuration[$"AI:{PrimaryTier}:ModelName"] ?? "gpt-4o";
        var apiVersion = _configuration[$"AI:{PrimaryTier}:ApiVersion"];

        if (string.IsNullOrEmpty(apiKey) || string.IsNullOrEmpty(endpoint))
        {
            _logger.LogWarning("AI {Tier} configuration missing — compliance-ask falls back to sources-only", PrimaryTier);
            return null;
        }

        try
        {
            var request = new
            {
                model = modelName,
                messages = new object[]
                {
                    new { role = "system", content = SystemPrompt },
                    new { role = "system", content = context },
                    new { role = "user", content = question }
                },
                temperature = 0.2,
                max_tokens = 1200
            };

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

            var httpClient = _httpClientFactory.CreateClient("AIService");
            var httpResponse = await httpClient.SendAsync(httpRequest, ct);
            if (!httpResponse.IsSuccessStatusCode)
            {
                _logger.LogWarning("Compliance-ask AI API returned {StatusCode}", httpResponse.StatusCode);
                return null;
            }

            var responseContent = await httpResponse.Content.ReadAsStringAsync(ct);
            using var responseJson = JsonDocument.Parse(responseContent);
            return responseJson.RootElement
                .GetProperty("choices")[0]
                .GetProperty("message")
                .GetProperty("content")
                .GetString();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Compliance-ask model call failed");
            return null;
        }
    }
}
