using IRETP.Application.DTOs;
using MediatR;

namespace IRETP.Application.Features.EWRS.Commands;

/// <summary>
/// Record a DLD officer toggling a playbook step for a specific RiskAlert
/// (RFP Section 8.3). The full progress array is sent back so the server
/// stores a canonical representation regardless of how the UI mutated it.
/// </summary>
public class UpdateAlertPlaybookProgressCommand : IRequest<bool>
{
    public Guid AlertId { get; set; }
    public List<PlaybookProgressEntry> Progress { get; set; } = [];
    public string? UpdatedBy { get; set; }
}
