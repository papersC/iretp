using System.Text.Json;

namespace IRETP.Web.Services;

/// <summary>
/// In-memory store for the Slice &amp; Dice analytics engine: saves, lists, and
/// shares analysis views. Shareable links serialise the full configuration
/// into a compact URL-safe token so recipients get an identical chart without
/// needing an account. Production persists via the SavedAnalyticsView entity.
/// </summary>
public class SavedViewsService
{
    public sealed record SavedView(
        Guid Id,
        string Name,
        string Owner,
        AnalyticsConfig Config,
        DateTime CreatedAt);

    /// <summary>Mutable analytics configuration (bound directly from Blazor pages).</summary>
    public sealed class AnalyticsConfig
    {
        public string Dimension { get; set; } = "Zone";      // Zone | Developer | PropertyType | Status
        public string Metric { get; set; } = "Value";         // Count | Value | AvgPrice | Yield | MarketShare
        public string ChartType { get; set; } = "bar";        // bar | stackedBar | line | area | scatter | donut | treemap | table | kpi
        public string? SecondaryDimension { get; set; }
        public int TopN { get; set; } = 10;
        public string? PropertyTypeFilter { get; set; }
        public string? StatusFilter { get; set; }

        public AnalyticsConfig() { }

        public AnalyticsConfig(string dimension, string metric, string chartType,
            string? secondaryDimension, int topN, string? propertyTypeFilter, string? statusFilter)
        {
            Dimension = dimension;
            Metric = metric;
            ChartType = chartType;
            SecondaryDimension = secondaryDimension;
            TopN = topN;
            PropertyTypeFilter = propertyTypeFilter;
            StatusFilter = statusFilter;
        }

        public AnalyticsConfig Clone() => new(Dimension, Metric, ChartType,
            SecondaryDimension, TopN, PropertyTypeFilter, StatusFilter);
    }

    private readonly List<SavedView> _views;

    public event Action? OnChange;

    public SavedViewsService()
    {
        _views =
        [
            new(Guid.NewGuid(), "Top zones by value",
                "seed",
                new AnalyticsConfig("Zone", "Value", "bar", null, 10, null, null),
                new DateTime(2026, 3, 20)),
            new(Guid.NewGuid(), "Developer share of market",
                "seed",
                new AnalyticsConfig("Developer", "MarketShare", "donut", null, 8, null, null),
                new DateTime(2026, 3, 22)),
        ];
    }

    /// <summary>
    /// RFP AN-003 — personal dashboard caps at 12 saved views. Mirrors the
    /// server-side cap enforced in <c>SaveAnalyticsViewCommandHandler</c>.
    /// </summary>
    public const int MaxSavedViews = 12;

    public IReadOnlyList<SavedView> List() => _views;

    public SavedView Save(string name, string owner, AnalyticsConfig config)
    {
        if (_views.Count >= MaxSavedViews)
        {
            throw new InvalidOperationException(
                $"Saved views are capped at {MaxSavedViews} (RFP AN-003). Delete one before saving another.");
        }

        var v = new SavedView(Guid.NewGuid(), name, owner, config, DateTime.UtcNow);
        _views.Insert(0, v);
        OnChange?.Invoke();
        return v;
    }

    public void Delete(Guid id)
    {
        var removed = _views.RemoveAll(v => v.Id == id);
        if (removed > 0) OnChange?.Invoke();
    }

    /// <summary>
    /// Move <paramref name="draggedId"/> to the position currently occupied
    /// by <paramref name="targetId"/>. No-op when either id is missing.
    /// Used by the drag-and-drop reorder gesture in the saved-views grid.
    /// </summary>
    public void Reorder(Guid draggedId, Guid targetId)
    {
        if (draggedId == targetId) return;

        var dragged = _views.FirstOrDefault(v => v.Id == draggedId);
        var target = _views.FirstOrDefault(v => v.Id == targetId);
        if (dragged is null || target is null) return;

        _views.Remove(dragged);
        var insertAt = _views.IndexOf(target);
        if (insertAt < 0) insertAt = 0;
        _views.Insert(insertAt, dragged);
        OnChange?.Invoke();
    }

    public string Encode(AnalyticsConfig config)
    {
        var json = JsonSerializer.Serialize(config);
        return Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(json))
            .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    public AnalyticsConfig? Decode(string token)
    {
        try
        {
            var padded = token.Replace('-', '+').Replace('_', '/');
            padded = padded.PadRight(padded.Length + (4 - padded.Length % 4) % 4, '=');
            var json = System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded));
            return JsonSerializer.Deserialize<AnalyticsConfig>(json);
        }
        catch
        {
            return null;
        }
    }

    public static readonly IReadOnlyList<string> ChartTypes = new[]
    {
        "bar", "stackedBar", "line", "area", "scatter", "donut", "treemap", "table", "kpi"
    };

    /// <summary>Heuristic for the "Recommended" chart badge.</summary>
    public static string Recommend(AnalyticsConfig c)
    {
        if (c.Metric == "MarketShare") return "donut";
        if (c.Metric == "Yield") return "line";
        if (c.SecondaryDimension != null) return "stackedBar";
        return c.ChartType;
    }
}
