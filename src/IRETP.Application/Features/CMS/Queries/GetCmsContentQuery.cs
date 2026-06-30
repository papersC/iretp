using MediatR;

namespace IRETP.Application.Features.CMS.Queries;

public class GetCmsContentQuery : IRequest<List<CmsContentDto>>
{
    public string? PageKey { get; set; }
    public string? SectionKey { get; set; }
    public string Locale { get; set; } = "en";
    public bool PublishedOnly { get; set; } = true;
}

public class CmsContentDto
{
    public Guid Id { get; set; }
    public string PageKey { get; set; } = default!;
    public string SectionKey { get; set; } = default!;
    public string ContentType { get; set; } = default!;
    public string Content { get; set; } = default!;
    public int SortOrder { get; set; }
    public bool IsPublished { get; set; }
    public DateTime? PublishedAt { get; set; }
    public int Version { get; set; }
}
