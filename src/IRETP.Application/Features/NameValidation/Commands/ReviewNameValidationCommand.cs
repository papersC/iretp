using MediatR;

namespace IRETP.Application.Features.NameValidation.Commands;

public class ReviewNameValidationCommand : IRequest<bool>
{
    public Guid Id { get; set; }

    /// <summary>0=Pending, 1=Validated, 2=Rejected, 3=NeedsCorrection.</summary>
    public int Status { get; set; }

    public string? OfficialNameAr { get; set; }
    public string? ReviewNotes { get; set; }
    public string? ReviewerId { get; set; }
    public string? ReviewerName { get; set; }
}
