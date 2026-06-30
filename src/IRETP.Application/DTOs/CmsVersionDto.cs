namespace IRETP.Application.DTOs;

public class CmsVersionDto
{
    public Guid Id { get; set; }
    public Guid CmsContentId { get; set; }
    public int VersionNumber { get; set; }
    public string ChangeType { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string ContentEn { get; set; } = default!;
    public string ContentAr { get; set; } = default!;
    public int SortOrder { get; set; }
    public string? Summary { get; set; }
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasActivePreview { get; set; }
}

public class CmsPreviewLinkDto
{
    public string Token { get; set; } = default!;
    public DateTime ExpiresAt { get; set; }
    public int VersionNumber { get; set; }
}

public class CmsPreviewContentDto
{
    public Guid CmsContentId { get; set; }
    public string PageKey { get; set; } = default!;
    public string SectionKey { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string ContentEn { get; set; } = default!;
    public string ContentAr { get; set; } = default!;
    public int VersionNumber { get; set; }
    public DateTime SnapshotAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
