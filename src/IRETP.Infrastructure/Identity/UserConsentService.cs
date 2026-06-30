using IRETP.Application.Interfaces;
using Microsoft.AspNetCore.Identity;

namespace IRETP.Infrastructure.Identity;

/// <summary>
/// Reads PDPL consent flags from the ASP.NET Identity user store. Kept out
/// of the Application layer so <c>AIOrchestrator</c> stays framework-agnostic.
/// </summary>
public class UserConsentService : IUserConsent
{
    private readonly UserManager<ApplicationUser> _userManager;

    public UserConsentService(UserManager<ApplicationUser> userManager)
    {
        _userManager = userManager;
    }

    public async Task<bool> HasAiMemoryConsentAsync(string? userId, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(userId)) return false;
        var user = await _userManager.FindByIdAsync(userId);
        return user?.ConsentAiMemory ?? false;
    }
}
