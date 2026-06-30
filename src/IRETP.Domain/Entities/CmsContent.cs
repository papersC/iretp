using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class CmsContent : BaseEntity
{
    public string PageKey { get; set; } = default!;
    public string SectionKey { get; set; } = default!;
    public string ContentType { get; set; } = default!; // RichText, Image, Banner, DataTable, Chart
    public string ContentEn { get; set; } = default!;
    public string ContentAr { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public string? PublishedBy { get; set; }
    public int Version { get; set; } = 1;
}
