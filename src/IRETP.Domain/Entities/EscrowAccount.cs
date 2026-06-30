using IRETP.Domain.Common;
using IRETP.Domain.Enums;

namespace IRETP.Domain.Entities;

public class EscrowAccount : BaseEntity
{
    public Guid ProjectId { get; set; }
    public Project Project { get; set; } = default!;
    public string AccountNumber { get; set; } = default!;
    public string BankName { get; set; } = default!;
    public decimal CurrentBalance { get; set; }
    public decimal RequiredMinimumBalance { get; set; }
    public decimal TotalFundsReceived { get; set; }
    public decimal TotalAuthorisedWithdrawals { get; set; }
    public decimal RemainingConstructionCost { get; set; }
    public EscrowStatus Status { get; set; }
    public decimal AdequacyRatio { get; set; }

    public ICollection<EscrowTransaction> EscrowTransactions { get; set; } = [];
}
