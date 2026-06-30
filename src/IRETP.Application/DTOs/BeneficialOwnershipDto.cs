namespace IRETP.Application.DTOs;

public class BeneficialOwnershipDto
{
    public Guid Id { get; set; }
    public Guid DeveloperId { get; set; }
    public string OwnerName { get; set; } = default!;
    public string? OwnerNameAr { get; set; }
    public string OwnerType { get; set; } = default!;
    public string? CountryOfIncorporation { get; set; }
    public decimal OwnershipPct { get; set; }
    public DateTime DisclosedAt { get; set; }
    public string? DisclosureSource { get; set; }
}
