using IRETP.Domain.Common;

namespace IRETP.Domain.Entities;

public class EscrowTransaction : BaseEntity
{
    public Guid EscrowAccountId { get; set; }
    public EscrowAccount EscrowAccount { get; set; } = default!;
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = default!; // Deposit, Withdrawal, Adjustment
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? AuthorisedBy { get; set; }
}
