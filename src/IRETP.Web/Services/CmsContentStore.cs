namespace IRETP.Web.Services;

/// <summary>
/// Minimal headless-CMS store used by both the public portal (read) and the
/// admin CMS editor (read/write). Keyed by (pageKey, sectionKey) and carries
/// English and Arabic bodies plus a staging-vs-published flag per RFP FR002.
/// Production swaps for the /api/cms controller wired to CmsContent entity.
/// </summary>
public class CmsContentStore
{
    public sealed class Content
    {
        public Guid Id { get; init; } = Guid.NewGuid();
        public string PageKey { get; set; } = "";
        public string SectionKey { get; set; } = "";
        public string ContentType { get; set; } = "RichText";
        public string ContentEn { get; set; } = "";
        public string ContentAr { get; set; } = "";
        public int SortOrder { get; set; }
        public bool IsPublished { get; set; }
        public DateTime? PublishedAt { get; set; }
        public int Version { get; set; } = 1;
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string UpdatedBy { get; set; } = "seed";
    }

    private readonly List<Content> _all;

    public event Action? OnChange;

    public CmsContentStore()
    {
        _all =
        [
            new()
            {
                PageKey = "home",
                SectionKey = "hero-banner",
                ContentType = "Banner",
                ContentEn = "Explore real-time real estate transparency across Dubai.",
                ContentAr = "استكشف الشفافية العقارية المباشرة في جميع أنحاء دبي.",
                SortOrder = 1,
                IsPublished = true,
                PublishedAt = new DateTime(2026, 3, 15),
            },
            new()
            {
                PageKey = "home",
                SectionKey = "kpi-callout",
                ContentType = "RichText",
                ContentEn = "Backed by the DLD registry, refreshed every 15 minutes.",
                ContentAr = "مدعوم بسجل دائرة الأراضي والأملاك، ويُحدَّث كل ١٥ دقيقة.",
                SortOrder = 2,
                IsPublished = true,
                PublishedAt = new DateTime(2026, 3, 15),
            },
            new()
            {
                PageKey = "map",
                SectionKey = "legend-note",
                ContentType = "RichText",
                ContentEn = "Zone boundaries sourced from Dubai Municipality GIS.",
                ContentAr = "حدود المناطق من نظم المعلومات الجغرافية لبلدية دبي.",
                SortOrder = 1,
                IsPublished = true,
                PublishedAt = new DateTime(2026, 3, 18),
            },
            new()
            {
                PageKey = "transactions",
                SectionKey = "disclaimer",
                ContentType = "RichText",
                ContentEn = "All figures exclude anonymised, pending, and voided records.",
                ContentAr = "جميع الأرقام تستثني السجلات المجهولة أو المعلقة أو الملغاة.",
                SortOrder = 1,
                IsPublished = false,
            },
        ];
    }

    public IReadOnlyList<Content> All() => _all;

    public IReadOnlyList<Content> ByPage(string pageKey) =>
        _all.Where(c => string.Equals(c.PageKey, pageKey, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.SortOrder).ToList();

    public Content? Find(Guid id) => _all.FirstOrDefault(c => c.Id == id);

    public void Upsert(Content content, string user)
    {
        var idx = _all.FindIndex(c => c.Id == content.Id);
        content.UpdatedAt = DateTime.UtcNow;
        content.UpdatedBy = user;
        if (idx < 0) _all.Add(content);
        else
        {
            content.Version = _all[idx].Version + 1;
            _all[idx] = content;
        }
        OnChange?.Invoke();
    }

    public bool Publish(Guid id, string user)
    {
        var idx = _all.FindIndex(c => c.Id == id);
        if (idx < 0) return false;
        _all[idx].IsPublished = true;
        _all[idx].PublishedAt = DateTime.UtcNow;
        _all[idx].UpdatedBy = user;
        _all[idx].UpdatedAt = DateTime.UtcNow;
        OnChange?.Invoke();
        return true;
    }

    public bool Delete(Guid id)
    {
        var removed = _all.RemoveAll(c => c.Id == id);
        if (removed > 0) { OnChange?.Invoke(); return true; }
        return false;
    }
}
