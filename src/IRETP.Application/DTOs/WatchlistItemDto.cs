namespace IRETP.Application.DTOs;

public class WatchlistItemDto
{
    public Guid Id { get; set; }
    public Guid? ProjectId { get; set; }
    public string? ProjectName { get; set; }
    public Guid? ZoneId { get; set; }
    public string? ZoneName { get; set; }
    public Guid? DeveloperId { get; set; }
    public string? DeveloperName { get; set; }
    public DateTime CreatedAt { get; set; }
}
