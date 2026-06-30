using System.Text;
using System.Text.RegularExpressions;
using IRETP.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services.Rag;

/// <summary>
/// One indexed record: its source entity, the human-readable text that was
/// embedded, and the normalized sparse TF-IDF vector (term-index -&gt; weight).
/// </summary>
public sealed class VectorDocument
{
    public string EntityType { get; init; } = "";
    public string EntityId { get; init; } = "";
    public string Content { get; init; } = "";
    /// <summary>L2-normalized sparse TF-IDF vector. Key = vocab term index.</summary>
    public Dictionary<int, float> Vector { get; set; } = new();
}

public interface IVectorStore
{
    Task EnsureIndexedAsync(CancellationToken ct = default);
    void Invalidate();
    int DocumentCount { get; }
    DateTime? LastIndexedUtc { get; }
    IReadOnlyList<(VectorDocument Doc, double Score)> Search(string query, int topK = 12);
}

/// <summary>
/// A real (classical) vector store for RAG over the IRETP DLD data. Each record
/// is turned into natural-language text, tokenized (English + Arabic), and
/// projected into a TF-IDF vector space; retrieval is cosine similarity against
/// the query vector. This is genuine vectorization — real vectors, real cosine
/// ranking — chosen because the configured Azure OpenAI resource exposes only a
/// chat deployment (no embeddings model). The <see cref="IVectorStore"/> seam
/// lets a neural embedder replace the TF-IDF projection without touching callers.
///
/// The index is process-wide (static) and built once, then reused across
/// requests; <see cref="Invalidate"/> forces a rebuild after data changes.
/// </summary>
public sealed class TfidfVectorStore : IVectorStore
{
    private readonly IretpDbContext _db;
    private readonly ILogger<TfidfVectorStore> _logger;

    private static readonly List<VectorDocument> _documents = new();
    private static readonly Dictionary<string, int> _vocab = new();   // term -> index
    private static double[] _idf = Array.Empty<double>();              // index -> idf
    private static readonly SemaphoreSlim _lock = new(1, 1);
    private static DateTime _lastIndexed = DateTime.MinValue;

    public TfidfVectorStore(IretpDbContext db, ILogger<TfidfVectorStore> logger)
    {
        _db = db;
        _logger = logger;
    }

    public int DocumentCount => _documents.Count;
    public DateTime? LastIndexedUtc => _lastIndexed == DateTime.MinValue ? null : _lastIndexed;
    private static bool NeedsReindex => _documents.Count == 0 || (DateTime.UtcNow - _lastIndexed).TotalMinutes > 30;

    public void Invalidate() => _lastIndexed = DateTime.MinValue;

    // ──────────────────────────────────────────────────────────────
    // Indexing
    // ──────────────────────────────────────────────────────────────

    public async Task EnsureIndexedAsync(CancellationToken ct = default)
    {
        if (!NeedsReindex) return;
        await _lock.WaitAsync(ct);
        try
        {
            if (!NeedsReindex) return; // double-check after acquiring the lock

            _logger.LogInformation("Vector store: building document corpus from DLD data...");
            var raw = new List<(string Type, string Id, string Content)>();
            await BuildCorpusAsync(raw, ct);

            // Pass 1: tokenize every doc, accumulate term frequencies + document frequency.
            var tokenized = new List<(string Type, string Id, string Content, Dictionary<string, int> Tf)>(raw.Count);
            var df = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (var (type, id, content) in raw)
            {
                var tf = new Dictionary<string, int>(StringComparer.Ordinal);
                foreach (var tok in Tokenize(content))
                    tf[tok] = tf.GetValueOrDefault(tok) + 1;
                tokenized.Add((type, id, content, tf));
                foreach (var term in tf.Keys)
                    df[term] = df.GetValueOrDefault(term) + 1;
            }

            // Build vocabulary + smoothed IDF.  idf = ln((N+1)/(df+1)) + 1
            var vocab = new Dictionary<string, int>(StringComparer.Ordinal);
            var idf = new double[df.Count];
            int n = Math.Max(tokenized.Count, 1);
            foreach (var (term, frequency) in df)
            {
                int idx = vocab.Count;
                vocab[term] = idx;
                idf[idx] = Math.Log((n + 1.0) / (frequency + 1.0)) + 1.0;
            }

            // Pass 2: build L2-normalized sparse vectors. weight = (1 + ln tf) * idf
            var docs = new List<VectorDocument>(tokenized.Count);
            foreach (var (type, id, content, tf) in tokenized)
            {
                var vec = new Dictionary<int, float>(tf.Count);
                double norm = 0;
                foreach (var (term, count) in tf)
                {
                    int idx = vocab[term];
                    double w = (1.0 + Math.Log(count)) * idf[idx];
                    vec[idx] = (float)w;
                    norm += w * w;
                }
                norm = Math.Sqrt(norm);
                if (norm > 0)
                    foreach (var key in vec.Keys.ToList())
                        vec[key] = (float)(vec[key] / norm);

                docs.Add(new VectorDocument { EntityType = type, EntityId = id, Content = content, Vector = vec });
            }

            _documents.Clear();
            _documents.AddRange(docs);
            _vocab.Clear();
            foreach (var kv in vocab) _vocab[kv.Key] = kv.Value;
            _idf = idf;
            _lastIndexed = DateTime.UtcNow;

            _logger.LogInformation("Vector store: indexed {Docs} documents, {Terms} vocabulary terms.",
                _documents.Count, _vocab.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector store: indexing failed.");
        }
        finally
        {
            _lock.Release();
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Search
    // ──────────────────────────────────────────────────────────────

    public IReadOnlyList<(VectorDocument Doc, double Score)> Search(string query, int topK = 12)
    {
        if (string.IsNullOrWhiteSpace(query) || _documents.Count == 0)
            return Array.Empty<(VectorDocument, double)>();

        // Build the normalized query vector in the indexed vocabulary space.
        var qtf = new Dictionary<int, double>();
        foreach (var tok in Tokenize(query))
        {
            if (_vocab.TryGetValue(tok, out var idx))
                qtf[idx] = qtf.GetValueOrDefault(idx) + 1;
        }
        if (qtf.Count == 0) return Array.Empty<(VectorDocument, double)>();

        var qvec = new Dictionary<int, double>(qtf.Count);
        double qnorm = 0;
        foreach (var (idx, count) in qtf)
        {
            double w = (1.0 + Math.Log(count)) * _idf[idx];
            qvec[idx] = w;
            qnorm += w * w;
        }
        qnorm = Math.Sqrt(qnorm);
        if (qnorm == 0) return Array.Empty<(VectorDocument, double)>();

        // Light intent-aware re-rank: nudge the right record TYPE up without
        // overriding cosine. Aggregate questions favour summary docs; an explicit
        // entity mention favours that entity type.
        var ql = query.ToLowerInvariant();
        bool agg = ContainsLiteral(ql, "how many", "total", "number", "count", "average", "summary",
            "overall", "most", "highest", "lowest", "top", "rank", "compare", "biggest", "largest",
            "كم", "إجمالي", "عدد", "متوسط", "أغلى", "أعلى", "أقل", "أكثر", "أفضل", "قارن");
        string? typeIntent =
            ContainsLiteral(ql, "developer", "builder", "مطور") ? "developer" :
            ContainsLiteral(ql, "violation", "compliance", "penalty", "breach", "مخالفة") ? "violation" :
            ContainsLiteral(ql, "off-plan", "off plan", "offplan", "project", "construction", "handover", "مشروع", "مخطط") ? "project" :
            ContainsLiteral(ql, "rent", "yield", "إيجار", "عائد") ? "rental-index" :
            ContainsLiteral(ql, "benchmark", "greti", "global city", "international") ? "benchmark" :
            ContainsLiteral(ql, "zone", "area", "community", "district", "منطقة") ? "zone" :
            ContainsLiteral(ql, "price", "sqft", "psf", "square foot", "expensive", "valuation", "سعر", "أغلى") ? "price-index" : null;

        // Cosine = dot(qNormalized, docNormalized). Iterate the smaller (query) side.
        var results = new List<(VectorDocument Doc, double Score)>(_documents.Count);
        foreach (var doc in _documents)
        {
            double dot = 0;
            foreach (var (idx, qw) in qvec)
                if (doc.Vector.TryGetValue(idx, out var dw))
                    dot += (qw / qnorm) * dw;
            if (dot <= 0) continue;
            if (agg && doc.EntityType == "summary") dot *= 1.6;
            if (typeIntent != null && doc.EntityType == typeIntent) dot *= 1.3;
            results.Add((doc, dot));
        }

        return results.OrderByDescending(r => r.Score).Take(topK).ToList();
    }

    private static bool ContainsLiteral(string text, params string[] needles)
        => needles.Any(n => text.Contains(n, StringComparison.Ordinal));

    // ──────────────────────────────────────────────────────────────
    // Corpus construction (DLD entities -> natural-language documents)
    // ──────────────────────────────────────────────────────────────

    private async Task BuildCorpusAsync(List<(string, string, string)> docs, CancellationToken ct)
    {
        var zones = await _db.Zones.AsNoTracking().ToListAsync(ct);
        var zoneName = zones.ToDictionary(z => z.Id, z => z.Name);

        var txnByZone = (await _db.Transactions.AsNoTracking()
            .GroupBy(t => t.ZoneId)
            .Select(g => new
            {
                ZoneId = g.Key,
                Count = g.Count(),
                AvgPsf = g.Average(t => t.PricePerSqft),
                Total = g.Sum(t => t.TransactionValue),
                MinPsf = g.Min(t => t.PricePerSqft),
                MaxPsf = g.Max(t => t.PricePerSqft)
            }).ToListAsync(ct)).ToDictionary(x => x.ZoneId);

        var projCountByZone = (await _db.Projects.AsNoTracking()
            .GroupBy(p => p.ZoneId).Select(g => new { ZoneId = g.Key, Count = g.Count() })
            .ToListAsync(ct)).ToDictionary(x => x.ZoneId, x => x.Count);

        var recentPrice = await _db.PriceIndices.AsNoTracking()
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).Take(400).ToListAsync(ct);
        var latestPriceByZone = recentPrice.GroupBy(p => p.ZoneId).ToDictionary(g => g.Key, g => g.First());

        var recentRent = await _db.RentalIndices.AsNoTracking()
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Quarter).Take(400).ToListAsync(ct);
        var latestRentByZone = recentRent.GroupBy(r => r.ZoneId).ToDictionary(g => g.Key, g => g.First());

        // ── Zone documents (enriched with transaction / price / rental aggregates)
        foreach (var z in zones)
        {
            var sb = new StringBuilder();
            sb.Append($"Zone / area: {z.Name} ({z.NameAr})");
            if (!string.IsNullOrWhiteSpace(z.ParentArea)) sb.Append($", part of {z.ParentArea}");
            sb.Append('.');
            if (txnByZone.TryGetValue(z.Id, out var tx))
                sb.Append($" Transactions: {tx.Count}, average price {tx.AvgPsf:N0} AED/sqft (range {tx.MinPsf:N0}-{tx.MaxPsf:N0}), total value {tx.Total:N0} AED.");
            sb.Append($" Projects: {projCountByZone.GetValueOrDefault(z.Id)}.");
            if (latestPriceByZone.TryGetValue(z.Id, out var pi))
                sb.Append($" Latest price index {pi.Year} Q{pi.Quarter} ({pi.PropertyType}): {pi.AveragePricePerSqft:N0} AED/sqft, annual change {pi.AnnualChange:+0.0;-0.0;0}%.");
            if (latestRentByZone.TryGetValue(z.Id, out var ri))
                sb.Append($" Latest rental yield {ri.GrossRentalYield:0.0}% gross (avg annual rent {ri.AverageAnnualRent:N0} AED).");
            docs.Add(("zone", z.Id.ToString(), sb.ToString()));
        }

        // ── Developer documents (score + violations + project count)
        var developers = await _db.Developers.AsNoTracking()
            .Include(d => d.Scores).Include(d => d.Violations).ToListAsync(ct);
        var projCountByDev = (await _db.Projects.AsNoTracking()
            .GroupBy(p => p.DeveloperId).Select(g => new { Id = g.Key, Count = g.Count() })
            .ToListAsync(ct)).ToDictionary(x => x.Id, x => x.Count);

        foreach (var d in developers)
        {
            var latest = d.Scores.OrderByDescending(s => s.Year).ThenByDescending(s => s.Quarter).FirstOrDefault();
            var sb = new StringBuilder();
            sb.Append($"Developer: {d.Name} ({d.NameAr}), licence {d.LicenceNumber}, {(d.IsActive ? "active" : "inactive")}.");
            sb.Append($" Projects: {projCountByDev.GetValueOrDefault(d.Id)}.");
            if (latest != null)
                sb.Append($" Latest developer score {latest.Year} Q{latest.Quarter}: composite {latest.CompositeScore:0.0}/100 " +
                          $"(on-time delivery {latest.OnTimeDeliveryScore:0}, escrow health {latest.EscrowHealthScore:0}, " +
                          $"regulatory compliance {latest.RegulatoryComplianceScore:0}, financial soundness {latest.FinancialSoundnessScore:0}).");
            if (d.Violations.Count > 0)
            {
                var worst = d.Violations.OrderByDescending(v => (int)v.Severity).First();
                sb.Append($" Regulatory violations: {d.Violations.Count} (worst severity: {worst.Severity}).");
            }
            else sb.Append(" Regulatory violations: none.");
            docs.Add(("developer", d.Id.ToString(), sb.ToString()));
        }

        // ── Project documents
        var projects = await _db.Projects.AsNoTracking()
            .Include(p => p.Developer).Include(p => p.Zone).Include(p => p.EscrowAccount)
            .OrderByDescending(p => p.CreatedAt).Take(250).ToListAsync(ct);
        foreach (var p in projects)
        {
            var sb = new StringBuilder();
            sb.Append($"Project: {p.Name} ({p.NameAr}) by {p.Developer?.Name ?? "unknown developer"} in {p.Zone?.Name ?? "unknown zone"}.");
            sb.Append($" Status: {p.Status}, {p.CompletionPercentage:0.#}% complete.");
            sb.Append($" Units: {p.TotalUnits} total, {p.SoldUnits} sold, {p.AvailableUnits} available.");
            if (p.ExpectedDeliveryDate.HasValue) sb.Append($" Expected delivery {p.ExpectedDeliveryDate:yyyy-MM}.");
            if (p.TotalProjectCost.HasValue) sb.Append($" Total cost {p.TotalProjectCost:N0} AED.");
            sb.Append($" Escrow account: {(p.EscrowAccount != null ? "yes" : "no")}.");
            if (!string.IsNullOrWhiteSpace(p.DldRegistrationNumber)) sb.Append($" DLD registration {p.DldRegistrationNumber}.");
            docs.Add(("project", p.Id.ToString(), sb.ToString()));
        }

        // ── Price index documents (recent)
        foreach (var pi in recentPrice.Take(150))
        {
            var zn = zoneName.GetValueOrDefault(pi.ZoneId, "unknown zone");
            docs.Add(("price-index", pi.Id.ToString(),
                $"Price index — {zn}, {pi.PropertyType}{(pi.IsOffPlan ? " off-plan" : "")}, {pi.Year} Q{pi.Quarter}: " +
                $"average {pi.AveragePricePerSqft:N0} AED/sqft from {pi.TransactionCount} transactions, total {pi.TotalValue:N0} AED, " +
                $"quarterly change {pi.QuarterlyChange:+0.0;-0.0;0}%, annual change {pi.AnnualChange:+0.0;-0.0;0}%."));
        }

        // ── Rental index documents (recent)
        foreach (var ri in recentRent.Take(150))
        {
            var zn = zoneName.GetValueOrDefault(ri.ZoneId, "unknown zone");
            docs.Add(("rental-index", ri.Id.ToString(),
                $"Rental index — {zn}, {ri.UnitType}{(ri.IsShortTerm ? " short-term" : "")}, {ri.Year} Q{ri.Quarter}: " +
                $"average annual rent {ri.AverageAnnualRent:N0} AED, gross yield {ri.GrossRentalYield:0.00}%, sample size {ri.SampleSize}."));
        }

        // ── International market benchmarks
        var benchmarks = await _db.MarketBenchmarks.AsNoTracking().ToListAsync(ct);
        foreach (var b in benchmarks)
        {
            docs.Add(("benchmark", b.Id.ToString(),
                $"International market benchmark — {b.CityName} ({b.CityCode}, {b.CountryCode}), {b.Year} Q{b.Quarter}: " +
                $"GRETI transparency {b.GretiCompositeScore:0.00}, average price {b.AveragePricePerSqft:N0} USD/sqft, " +
                $"gross rental yield {b.AverageGrossRentalYieldPct:0.0}%, prime price YoY {b.PrimePriceYoYPct:+0.0;-0.0;0}%, " +
                $"transaction volume YoY {b.TransactionVolumeYoYPct:+0.0;-0.0;0}%, institutional capital share {b.InstitutionalCapitalSharePct:0.0}%."));
        }

        // ── Regulatory violations (recent)
        var violations = await _db.RegulatoryViolations.AsNoTracking()
            .Include(v => v.Developer).OrderByDescending(v => v.ViolationDate).Take(200).ToListAsync(ct);
        foreach (var v in violations)
        {
            docs.Add(("violation", v.Id.ToString(),
                $"Regulatory violation — developer {v.Developer?.Name ?? "unknown"}, {v.ViolationDate:yyyy-MM-dd}, severity {v.Severity}: " +
                $"{v.Description}{(string.IsNullOrWhiteSpace(v.IssuedBy) ? "" : $" Issued by {v.IssuedBy}.")}"));
        }

        await AddSummaryDocsAsync(docs, ct);
    }

    private async Task AddSummaryDocsAsync(List<(string, string, string)> docs, CancellationToken ct)
    {
        int zones = await _db.Zones.CountAsync(ct);
        int developers = await _db.Developers.CountAsync(ct);
        int projects = await _db.Projects.CountAsync(ct);
        int txns = await _db.Transactions.CountAsync(ct);
        int violations = await _db.RegulatoryViolations.CountAsync(ct);
        int benchmarks = await _db.MarketBenchmarks.CountAsync(ct);
        decimal avgPsf = txns > 0 ? await _db.Transactions.AverageAsync(t => t.PricePerSqft, ct) : 0;
        decimal totalVal = txns > 0 ? await _db.Transactions.SumAsync(t => t.TransactionValue, ct) : 0;

        docs.Add(("summary", "global",
            $"PLATFORM SUMMARY: {zones} zones, {developers} developers, {projects} projects, {txns} transactions, " +
            $"average price {avgPsf:N0} AED/sqft, total transaction value {totalVal:N0} AED, " +
            $"{violations} regulatory violations on record, {benchmarks} international benchmark snapshots."));

        var byType = await _db.Transactions.AsNoTracking()
            .GroupBy(t => t.PropertyType).Select(g => new { Type = g.Key, Count = g.Count() }).ToListAsync(ct);
        if (byType.Count > 0)
            docs.Add(("summary", "txn-by-property-type",
                "SUMMARY: transactions by property type — " + string.Join(", ", byType.Select(x => $"{x.Type}: {x.Count}")) + "."));

        var byTxnType = await _db.Transactions.AsNoTracking()
            .GroupBy(t => t.TransactionType).Select(g => new { Type = g.Key, Count = g.Count() }).ToListAsync(ct);
        if (byTxnType.Count > 0)
            docs.Add(("summary", "txn-by-type",
                "SUMMARY: transactions by type — " + string.Join(", ", byTxnType.Select(x => $"{x.Type}: {x.Count}")) + "."));

        var topZones = (await _db.Transactions.AsNoTracking()
            .GroupBy(t => t.ZoneId)
            .Select(g => new { g.Key, Avg = g.Average(t => t.PricePerSqft), Count = g.Count() })
            .ToListAsync(ct))
            .OrderByDescending(x => x.Avg).Take(10).ToList();
        if (topZones.Count > 0)
        {
            var names = (await _db.Zones.AsNoTracking().ToListAsync(ct)).ToDictionary(z => z.Id, z => z.Name);
            docs.Add(("summary", "top-zones-by-price",
                "SUMMARY: highest average price per sqft by zone — " +
                string.Join(", ", topZones.Select(x => $"{names.GetValueOrDefault(x.Key, "?")}: {x.Avg:N0} AED/sqft ({x.Count} txns)")) + "."));
        }

        // Off-plan share, index coverage, the price extreme, and rental-yield
        // leaders — these answer "how many / highest" questions the model would
        // otherwise wrongly infer from the small retrieved sample.
        int offPlan = await _db.Transactions.CountAsync(t => t.IsOffPlan, ct);
        docs.Add(("summary", "offplan",
            $"SUMMARY: {offPlan} of {txns} transactions are off-plan ({(txns > 0 ? 100.0 * offPlan / txns : 0):0.#}%); {txns - offPlan} are ready/secondary-market."));

        int priceIdxCount = await _db.PriceIndices.CountAsync(ct);
        int rentIdxCount = await _db.RentalIndices.CountAsync(ct);
        docs.Add(("summary", "index-coverage",
            $"SUMMARY: index coverage — {priceIdxCount} price-index records and {rentIdxCount} rental-index records across zones, property types and quarters."));

        var topTxn = await _db.Transactions.AsNoTracking()
            .OrderByDescending(t => t.PricePerSqft)
            .Select(t => new { t.PricePerSqft, Zone = t.Zone.Name })
            .FirstOrDefaultAsync(ct);
        if (topTxn != null)
            docs.Add(("summary", "max-price",
                $"SUMMARY: the highest single transaction price on record is {topTxn.PricePerSqft:N0} AED/sqft in {topTxn.Zone}."));

        var topYield = (await _db.RentalIndices.AsNoTracking()
            .GroupBy(r => r.ZoneId)
            .Select(g => new { g.Key, Avg = g.Average(r => r.GrossRentalYield) })
            .ToListAsync(ct))
            .OrderByDescending(x => x.Avg).Take(5).ToList();
        if (topYield.Count > 0)
        {
            var zn = (await _db.Zones.AsNoTracking().ToListAsync(ct)).ToDictionary(z => z.Id, z => z.Name);
            docs.Add(("summary", "top-rental-yield",
                "SUMMARY: highest gross rental yields by zone — " +
                string.Join(", ", topYield.Select(x => $"{zn.GetValueOrDefault(x.Key, "?")}: {x.Avg:0.0}%")) + "."));
        }
    }

    // ──────────────────────────────────────────────────────────────
    // Tokenization (English + Arabic, light stemming + synonym expansion)
    // ──────────────────────────────────────────────────────────────

    private static readonly char[] Separators =
        { ' ', ',', '.', '?', '!', ';', ':', '(', ')', '[', ']', '{', '}', '/', '\\', '-', '_', '\t', '\n', '\r', '"', '\'', '%', '+', '&' };

    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "the","a","an","is","are","was","were","be","been","being","have","has","had","do","does","did",
        "will","would","could","should","may","might","can","how","many","much","what","which","who","whose",
        "where","when","why","that","this","these","those","i","me","my","we","our","you","your","it","its",
        "they","them","their","of","in","to","for","with","on","at","by","from","as","and","but","or","not",
        "no","so","if","then","than","too","very","all","each","some","such","only","also","just","about",
        "over","under","show","tell","give","get","list","please","me",
        "كم","ما","هل","عدد","كيف","من","في","على","إلى","عن","أن","هذا","هذه","ذلك","تلك","التي","الذي",
        "هو","هي","هم","نحن","أنا","أنت","لا","لم","لن","قد","كان","كانت","يكون","مع","بين","أو","و"
    };

    private static readonly Dictionary<string, string[]> Synonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        { "price", new[] { "psf", "sqft", "value", "expensive", "سعر", "أسعار" } },
        { "expensive", new[] { "price", "sqft", "high" } },
        { "sqft", new[] { "price", "psf" } },
        { "rent", new[] { "rental", "yield", "إيجار", "عائد" } },
        { "yield", new[] { "rent", "rental", "عائد" } },
        { "developer", new[] { "builder", "مطور", "مطورين" } },
        { "project", new[] { "offplan", "development", "مشروع", "مشاريع" } },
        { "offplan", new[] { "project", "preconstruction" } },
        { "zone", new[] { "area", "community", "district", "منطقة", "حي" } },
        { "area", new[] { "zone", "community", "district" } },
        { "community", new[] { "zone", "area" } },
        { "transaction", new[] { "sale", "deal", "صفقة", "معاملة" } },
        { "violation", new[] { "penalty", "breach", "compliance", "مخالفة" } },
        { "compliance", new[] { "violation", "regulatory" } },
        { "escrow", new[] { "ضمان" } },
        { "villa", new[] { "فيلا" } },
        { "apartment", new[] { "flat", "شقة" } },
        { "benchmark", new[] { "city", "global", "international" } },
        // Arabic -> English so Arabic queries retrieve the English data documents.
        { "معاملات", new[] { "transaction", "sale" } },
        { "معاملة", new[] { "transaction" } },
        { "صفقة", new[] { "transaction", "deal" } },
        { "صفقات", new[] { "transaction" } },
        { "منطقة", new[] { "zone", "area" } },
        { "مناطق", new[] { "zone", "area" } },
        { "مطور", new[] { "developer" } },
        { "مطورين", new[] { "developer" } },
        { "سعر", new[] { "price", "sqft" } },
        { "أسعار", new[] { "price" } },
        { "عائد", new[] { "yield", "rent" } },
        { "إيجار", new[] { "rent", "yield" } },
        { "مشروع", new[] { "project" } },
        { "مشاريع", new[] { "project" } },
        { "عقار", new[] { "property", "estate" } },
        { "عقارية", new[] { "property", "estate" } },
        { "مخطط", new[] { "offplan", "project" } },
    };

    private static IEnumerable<string> Tokenize(string text)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var outp = new List<string>();
        // Adds a token and (recursively) its synonyms, so expansion also applies
        // to stemmed/normalized forms — e.g. Arabic "المعاملات" -> "معاملات" ->
        // "transaction" lets an Arabic query retrieve the English data documents.
        void Add(string t)
        {
            if (t.Length < 2 || StopWords.Contains(t) || !seen.Add(t)) return;
            outp.Add(t);
            if (Synonyms.TryGetValue(t, out var syn)) foreach (var s in syn) Add(s);
        }
        foreach (var raw in Canonicalize(text).Split(Separators, StringSplitOptions.RemoveEmptyEntries))
        {
            if (raw.Length < 2 || StopWords.Contains(raw)) continue;
            Add(raw);
            // Light English plural stemming: areas->area, developers->developer.
            if (raw.Length > 3 && raw.EndsWith("s") && !raw.EndsWith("ss")) Add(raw[..^1]);
            // Arabic light normalization: strip leading "ال", unify alef/taa-marbuta.
            if (raw.StartsWith("ال") && raw.Length > 3) Add(raw.Substring(2));
            var norm = raw.Replace('أ', 'ا').Replace('إ', 'ا').Replace('آ', 'ا').Replace('ة', 'ه');
            if (norm != raw) Add(norm);
        }
        return outp;
    }

    /// <summary>
    /// Canonicalizes multi-word domain phrases/units into single tokens BEFORE
    /// splitting, so e.g. "square foot" stops colliding with "Town Square", and
    /// price/rent units match regardless of phrasing. Applied to both documents
    /// and queries so the spaces line up.
    /// </summary>
    private static string Canonicalize(string text)
    {
        var t = text.ToLowerInvariant();
        t = Regex.Replace(t, @"(price\s+)?per\s+square\s+(foot|feet)", " sqft ");
        t = Regex.Replace(t, @"square\s+(foot|feet)", " sqft ");
        t = Regex.Replace(t, @"\bsq\s*\.?\s*ft\b", " sqft ");
        t = Regex.Replace(t, @"\bpsf\b", " sqft ");
        t = t.Replace("/sqft", " sqft ");
        t = Regex.Replace(t, @"off[\s\-]+plan", " offplan ");
        t = Regex.Replace(t, @"(gross\s+)?rental\s+yield", " yield ");
        t = Regex.Replace(t, @"gross\s+yield", " yield ");
        t = Regex.Replace(t, @"annual\s+rent\b", " rent ");
        return t;
    }
}
