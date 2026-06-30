using MediatR;

namespace IRETP.Application.Features.CMS.Commands;

public class UpdateCmsContentCommand : IRequest<Guid>
{
    public Guid? Id { get; set; }
    public string PageKey { get; set; } = default!;
    public string SectionKey { get; set; } = default!;
    public string ContentType { get; set; } = "RichText";
    public string ContentEn { get; set; } = default!;
    public string ContentAr { get; set; } = default!;
    public int SortOrder { get; set; }
    public string? ChangeSummary { get; set; }
    public string? UserId { get; set; }
}
