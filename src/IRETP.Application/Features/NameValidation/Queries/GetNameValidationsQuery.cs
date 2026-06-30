using IRETP.Application.Common;
using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.NameValidation.Queries;

public class GetNameValidationsQuery : IRequest<PagedResult<NameValidationDto>>
{
    public string? EntityType { get; set; }
    public int? Status { get; set; }
    public string? Search { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 50;
}

public class GetNameValidationSummaryQuery : IRequest<NameValidationSummaryDto>
{
}
