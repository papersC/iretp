namespace IRETP.Application.DTOs;

public class UserAdminDto
{
    public string Id { get; set; } = default!;
    public string? Email { get; set; }
    public string? UserName { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PreferredLanguage { get; set; } = "en";
    public bool IsInternalUser { get; set; }
    public bool EmailConfirmed { get; set; }
    public bool LockoutEnabled { get; set; }
    public DateTime? LockoutEnd { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public List<string> Roles { get; set; } = [];
}
