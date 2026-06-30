namespace IRETP.Application.DTOs;

public class InvestorAlertDto
{
    public Guid Id { get; set; }
    public string AlertType { get; set; } = default!;
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public Guid? DeveloperId { get; set; }
    public Guid? ProjectId { get; set; }
    public decimal? ThresholdValue { get; set; }
    public string? ThresholdDirection { get; set; }
    public string? Frequency { get; set; }
    public bool IsEmailEnabled { get; set; }
    public bool IsSmsEnabled { get; set; }
    public bool IsPushEnabled { get; set; }
    public bool IsActive { get; set; }
}
