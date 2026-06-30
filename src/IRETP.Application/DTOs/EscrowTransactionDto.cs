namespace IRETP.Application.DTOs;

public class EscrowTransactionDto
{
    public Guid Id { get; set; }
    public Guid EscrowAccountId { get; set; }
    public DateTime TransactionDate { get; set; }
    public string TransactionType { get; set; } = default!;
    public decimal Amount { get; set; }
    public decimal BalanceAfter { get; set; }
    public string? Reference { get; set; }
    public string? AuthorisedBy { get; set; }
}
