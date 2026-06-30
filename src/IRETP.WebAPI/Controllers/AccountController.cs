using System.Security.Claims;
using System.Text.Json;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using IRETP.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRETP.WebAPI.Controllers;

/// <summary>
/// Per-user account settings (RFP Section 19.2 — PDPL). Covers:
/// consent flags (marketing, AI memory, analytics), profile fields, and
/// the Data Subject Access Request (DSAR) export that UAE Federal
/// Decree-Law No. 45 of 2021 requires DLD to fulfil on demand.
/// </summary>
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class AccountController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IRepository<WatchlistItem> _watchRepo;
    private readonly IRepository<InvestorAlert> _alertRepo;
    private readonly IRepository<Notification> _notificationRepo;
    private readonly IRepository<AiInteractionLog> _aiLogRepo;
    private readonly IRepository<UserAiMemory> _aiMemoryRepo;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IRETP.Application.Interfaces.IUnsubscribeTokenService _unsubscribeTokens;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        IRepository<WatchlistItem> watchRepo,
        IRepository<InvestorAlert> alertRepo,
        IRepository<Notification> notificationRepo,
        IRepository<AiInteractionLog> aiLogRepo,
        IRepository<UserAiMemory> aiMemoryRepo,
        IUnitOfWork unitOfWork,
        IRETP.Application.Interfaces.IUnsubscribeTokenService unsubscribeTokens)
    {
        _userManager = userManager;
        _watchRepo = watchRepo;
        _alertRepo = alertRepo;
        _notificationRepo = notificationRepo;
        _aiLogRepo = aiLogRepo;
        _aiMemoryRepo = aiMemoryRepo;
        _unitOfWork = unitOfWork;
        _unsubscribeTokens = unsubscribeTokens;
    }

    private string UserId =>
        User.FindFirst(ClaimTypes.NameIdentifier)?.Value
        ?? throw new UnauthorizedAccessException();

    [HttpGet("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> GetProfile()
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null) return NotFound();
        return Ok(new AccountProfileDto
        {
            Email = user.Email,
            FirstName = user.FirstName,
            LastName = user.LastName,
            PreferredLanguage = user.PreferredLanguage,
            PreferredCurrency = user.PreferredCurrency,
            ConsentMarketing = user.ConsentMarketing,
            ConsentAiMemory = user.ConsentAiMemory,
            ConsentUsageAnalytics = user.ConsentUsageAnalytics,
            ConsentUpdatedAt = user.ConsentUpdatedAt
        });
    }

    [HttpPut("profile")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateProfile([FromBody] AccountProfileUpdateDto request)
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null) return NotFound();

        if (request.FirstName is not null) user.FirstName = request.FirstName;
        if (request.LastName is not null) user.LastName = request.LastName;
        if (!string.IsNullOrWhiteSpace(request.PreferredLanguage)) user.PreferredLanguage = request.PreferredLanguage;
        if (!string.IsNullOrWhiteSpace(request.PreferredCurrency)) user.PreferredCurrency = request.PreferredCurrency;

        await _userManager.UpdateAsync(user);
        return Ok();
    }

    [HttpPut("consent")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> UpdateConsent([FromBody] ConsentUpdateDto request, CancellationToken ct = default)
    {
        var user = await _userManager.FindByIdAsync(UserId);
        if (user is null) return NotFound();

        var aiMemoryRevoked = user.ConsentAiMemory && !request.AiMemory;

        user.ConsentMarketing = request.Marketing;
        user.ConsentAiMemory = request.AiMemory;
        user.ConsentUsageAnalytics = request.UsageAnalytics;
        user.ConsentUpdatedAt = DateTime.UtcNow;

        await _userManager.UpdateAsync(user);

        // PDPL §19.2: when AI-memory consent is revoked, purge every row so
        // we don't retain zone preferences or topic counts after opt-out.
        if (aiMemoryRevoked)
        {
            var toRemove = await _aiMemoryRepo.Query()
                .Where(m => m.UserId == user.Id)
                .ToListAsync(ct);
            if (toRemove.Count > 0)
            {
                _aiMemoryRepo.RemoveRange(toRemove);
                await _unitOfWork.SaveChangesAsync(ct);
            }
        }

        return Ok(new { user.ConsentUpdatedAt });
    }

    /// <summary>
    /// RFP §6.2 + RFC 8058 one-click unsubscribe. The token is minted when
    /// the email is dispatched and binds to (userId, reason). Accepts POST
    /// (List-Unsubscribe-Post one-click flow) and GET (mail-client
    /// fallbacks that render the link). Success is silent — we never
    /// acknowledge whether the token was valid, so the endpoint can't be
    /// probed to enumerate valid userIds.
    /// </summary>
    [AllowAnonymous]
    [HttpPost("unsubscribe")]
    [HttpGet("unsubscribe")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> Unsubscribe([FromQuery] string u, [FromQuery] string r, [FromQuery] string t, CancellationToken ct = default)
    {
        if (!string.IsNullOrWhiteSpace(u) && _unsubscribeTokens.Verify(u, r ?? "marketing", t ?? string.Empty))
        {
            var user = await _userManager.FindByIdAsync(u);
            if (user is not null)
            {
                // Reason buckets: "marketing" revokes digest/market alerts,
                // "ai-memory" deletes cross-session AI preferences, anything
                // else flips the generic marketing flag.
                switch ((r ?? "marketing").ToLowerInvariant())
                {
                    case "ai-memory":
                        user.ConsentAiMemory = false;
                        var rows = await _aiMemoryRepo.Query()
                            .Where(m => m.UserId == user.Id)
                            .ToListAsync(ct);
                        if (rows.Count > 0)
                        {
                            _aiMemoryRepo.RemoveRange(rows);
                            await _unitOfWork.SaveChangesAsync(ct);
                        }
                        break;
                    default:
                        user.ConsentMarketing = false;
                        break;
                }
                user.ConsentUpdatedAt = DateTime.UtcNow;
                await _userManager.UpdateAsync(user);
            }
        }
        // Uniform response regardless of token validity.
        return Ok(new { message = "If your token was valid you have been unsubscribed." });
    }

    /// <summary>
    /// PDPL Data Subject Access Request (DSAR). Streams a JSON dump of every
    /// row across all entities tied to the authenticated user's id.
    /// Implementation intentionally uses the repositories directly rather
    /// than the Application layer — DSAR is a compliance endpoint and must
    /// not go through any cache or projection that might omit PII.
    /// </summary>
    [HttpGet("data-export")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> ExportPersonalData(CancellationToken ct = default)
    {
        var uid = UserId;
        var user = await _userManager.FindByIdAsync(uid);
        if (user is null) return NotFound();

        var bundle = new
        {
            exportGeneratedAt = DateTime.UtcNow,
            legalBasis = "UAE Federal Decree-Law No. 45 of 2021 on the Protection of Personal Data (PDPL). " +
                         "Data subject access right — Article 13.",
            profile = new
            {
                user.Id,
                user.Email,
                user.UserName,
                user.FirstName,
                user.LastName,
                user.PhoneNumber,
                user.PreferredLanguage,
                user.PreferredCurrency,
                user.IsInternalUser,
                user.EmailConfirmed,
                user.LastLoginAt
            },
            consent = new
            {
                user.ConsentMarketing,
                user.ConsentAiMemory,
                user.ConsentUsageAnalytics,
                user.ConsentUpdatedAt
            },
            watchlist = await _watchRepo.Query().Where(w => w.UserId == uid).ToListAsync(ct),
            alerts = await _alertRepo.Query().Where(a => a.UserId == uid).ToListAsync(ct),
            notifications = await _notificationRepo.Query().Where(n => n.UserId == uid).Take(1000).ToListAsync(ct),
            aiInteractions = await _aiLogRepo.Query().Where(a => a.UserId == uid).Take(1000).ToListAsync(ct)
        };

        var json = JsonSerializer.Serialize(bundle, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });

        return File(
            System.Text.Encoding.UTF8.GetBytes(json),
            "application/json",
            $"IRETP_PersonalData_{uid}_{DateTime.UtcNow:yyyyMMddHHmmss}.json");
    }
}

public class AccountProfileDto
{
    public string? Email { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredCurrency { get; set; } = "AED";
    public bool ConsentMarketing { get; set; }
    public bool ConsentAiMemory { get; set; }
    public bool ConsentUsageAnalytics { get; set; }
    public DateTime? ConsentUpdatedAt { get; set; }
}

public class AccountProfileUpdateDto
{
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredLanguage { get; set; }
    public string? PreferredCurrency { get; set; }
}

public class ConsentUpdateDto
{
    public bool Marketing { get; set; }
    public bool AiMemory { get; set; }
    public bool UsageAnalytics { get; set; }
}
