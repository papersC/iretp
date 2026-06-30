using IRETP.Domain.Enums;

namespace IRETP.Application.DTOs;

public class EscrowDashboardDto
{
    public Guid EscrowAccountId { get; set; }
    public Guid ProjectId { get; set; }
    public string ProjectName { get; set; } = default!;
    public string DeveloperName { get; set; } = default!;
    public string AccountNumber { get; set; } = default!;
    public string BankName { get; set; } = default!;
    public decimal CurrentBalance { get; set; }
    public decimal RequiredMinimumBalance { get; set; }
    public decimal TotalFundsReceived { get; set; }
    public decimal TotalAuthorisedWithdrawals { get; set; }
    public decimal RemainingConstructionCost { get; set; }
    public EscrowStatus Status { get; set; }
    public decimal AdequacyRatio { get; set; }
    public string StatusBadge { get; set; } = default!;
}
