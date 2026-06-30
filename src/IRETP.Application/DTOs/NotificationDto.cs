namespace IRETP.Application.DTOs;

public class NotificationDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = default!;
    public string TitleAr { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string MessageAr { get; set; } = default!;
    public string? Link { get; set; }
    public string Channel { get; set; } = default!;
    public string? Category { get; set; }
    public bool IsRead { get; set; }
    public DateTime? ReadAt { get; set; }
    public DateTime CreatedAt { get; set; }
}
