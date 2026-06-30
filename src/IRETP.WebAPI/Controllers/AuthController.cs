using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using IRETP.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly IConfiguration _configuration;
    private readonly IServiceScopeFactory _scopeFactory;

    public AuthController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        IConfiguration configuration,
        IServiceScopeFactory scopeFactory)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _configuration = configuration;
        _scopeFactory = scopeFactory;
    }

    /// <summary>
    /// Register a new public user account.
    /// </summary>
    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register([FromBody] RegisterRequest request)
    {
        var existingUser = await _userManager.FindByEmailAsync(request.Email);
        if (existingUser is not null)
            return BadRequest(new { message = "A user with this email already exists." });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName,
            LastName = request.LastName,
            PreferredLanguage = request.PreferredLanguage ?? "en"
        };

        var result = await _userManager.CreateAsync(user, request.Password);
        if (!result.Succeeded)
            return BadRequest(new { message = "Registration failed.", errors = result.Errors });

        await _userManager.AddToRoleAsync(user, Domain.Enums.UserRoles.RegisteredInvestor);

        return Ok(new { message = "Registration successful.", userId = user.Id });
    }

    /// <summary>
    /// Authenticate. For internal DLD staff, MFA is mandatory (RFP 10.2.1 /
    /// 10.3) — the response indicates whether a TOTP code is still required
    /// (or whether the user must first enroll an authenticator).
    /// </summary>
    [HttpPost("login")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var user = await _userManager.FindByEmailAsync(request.Email);
        if (user is null)
            return Unauthorized(new { message = "Invalid email or password." });

        var signInResult = await _signInManager.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!signInResult.Succeeded)
            return Unauthorized(new { message = "Invalid email or password." });

        // Internal DLD staff MUST use MFA per RFP 10.2.1. If they haven't
        // enrolled a TOTP authenticator yet, return a short-lived enrolment
        // ticket so the UI can guide them through setup before issuing real
        // tokens. Public investors are also allowed to opt in to MFA — if
        // enabled, they must complete the challenge to receive access tokens.
        // MFA is mandatory for internal DLD staff in production (RFP 10.2.1).
        // For local demo / dev runs it can be switched off with Auth:RequireMfa
        // = false (set only in appsettings.Development.json) so the internal
        // screens are reachable with password-only login. The flag DEFAULTS to
        // true when absent, so production behaviour is unchanged.
        var requireMfa = true;
        if (bool.TryParse(_configuration["Auth:RequireMfa"], out var rm)) requireMfa = rm;

        if (requireMfa && user.IsInternalUser && !user.TwoFactorEnabled)
        {
            var enrolmentTicket = GenerateMfaTicket(user.Id, "enrol");
            return Ok(new
            {
                requiresMfaSetup = true,
                mfaSetupToken = enrolmentTicket,
                message = "MFA enrollment is required for internal users before login can complete."
            });
        }

        if (requireMfa && user.TwoFactorEnabled)
        {
            var challengeTicket = GenerateMfaTicket(user.Id, "challenge");
            return Ok(new
            {
                requiresTwoFactor = true,
                twoFactorToken = challengeTicket
            });
        }

        return await IssueTokensAsync(user);
    }

    /// <summary>
    /// Complete login after the password step by submitting the TOTP code
    /// generated by the user's authenticator app.
    /// </summary>
    [HttpPost("login-2fa")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> LoginTwoFactor([FromBody] LoginTwoFactorRequest request)
    {
        var (userId, purpose) = ValidateMfaTicket(request.TwoFactorToken);
        if (userId is null || purpose != "challenge")
            return Unauthorized(new { message = "MFA token is invalid or expired." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null || !user.TwoFactorEnabled)
            return Unauthorized(new { message = "MFA is not enabled for this user." });

        var ok = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, request.Code);
        if (!ok)
            return Unauthorized(new { message = "Invalid authenticator code." });

        return await IssueTokensAsync(user);
    }

    /// <summary>
    /// Begin TOTP authenticator enrolment using a short-lived setup ticket
    /// returned by /login. Returns the shared secret and an otpauth:// URI
    /// that the UI can render as a QR code.
    /// </summary>
    [HttpPost("2fa/setup")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> SetupTwoFactor([FromBody] MfaSetupRequest request)
    {
        var (userId, purpose) = ValidateMfaTicket(request.MfaSetupToken);
        if (userId is null || purpose != "enrol")
            return Unauthorized(new { message = "Setup ticket is invalid or expired." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var key = await _userManager.GetAuthenticatorKeyAsync(user);
        if (string.IsNullOrEmpty(key))
        {
            await _userManager.ResetAuthenticatorKeyAsync(user);
            key = await _userManager.GetAuthenticatorKeyAsync(user);
        }

        var issuer = Uri.EscapeDataString(_configuration["Jwt:Issuer"] ?? "IRETP");
        var account = Uri.EscapeDataString(user.Email ?? user.UserName ?? user.Id);
        var otpAuthUri = $"otpauth://totp/{issuer}:{account}?secret={key}&issuer={issuer}&algorithm=SHA1&digits=6&period=30";

        return Ok(new { sharedKey = key, otpAuthUri });
    }

    /// <summary>
    /// Confirm the TOTP code generated by the authenticator app, enable MFA on
    /// the account, and complete login by issuing access tokens.
    /// </summary>
    [HttpPost("2fa/enable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> EnableTwoFactor([FromBody] MfaEnableRequest request)
    {
        var (userId, purpose) = ValidateMfaTicket(request.MfaSetupToken);
        if (userId is null || purpose != "enrol")
            return Unauthorized(new { message = "Setup ticket is invalid or expired." });

        var user = await _userManager.FindByIdAsync(userId);
        if (user is null) return Unauthorized();

        var ok = await _userManager.VerifyTwoFactorTokenAsync(
            user, _userManager.Options.Tokens.AuthenticatorTokenProvider, request.Code);
        if (!ok) return Unauthorized(new { message = "Invalid authenticator code." });

        await _userManager.SetTwoFactorEnabledAsync(user, true);
        return await IssueTokensAsync(user);
    }

    /// <summary>
    /// Refresh an expired JWT using a valid refresh token. Implements token rotation.
    /// </summary>
    [HttpPost("refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request)
    {
        using var scope = _scopeFactory.CreateScope();
        var refreshTokenRepo = scope.ServiceProvider.GetRequiredService<IRepository<RefreshToken>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var existingToken = await refreshTokenRepo.Query()
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

        if (existingToken == null || !existingToken.IsActive)
            return Unauthorized(new { message = "Invalid or expired refresh token." });

        // Revoke old token (token rotation)
        existingToken.RevokedAt = DateTime.UtcNow;
        existingToken.RevokedByIp = GetIpAddress();
        refreshTokenRepo.Update(existingToken);

        // Generate new refresh token
        var user = await _userManager.FindByIdAsync(existingToken.UserId);
        if (user == null)
            return Unauthorized(new { message = "User not found." });

        var newRefreshToken = GenerateRefreshTokenString();
        var newRefreshTokenEntity = new RefreshToken
        {
            UserId = user.Id,
            Token = newRefreshToken,
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7")),
            CreatedByIp = GetIpAddress()
        };

        existingToken.ReplacedByToken = newRefreshToken;

        await refreshTokenRepo.AddAsync(newRefreshTokenEntity);
        await unitOfWork.SaveChangesAsync();

        var accessToken = await GenerateJwtToken(user);

        return Ok(new
        {
            accessToken,
            refreshToken = newRefreshToken,
            expiresIn = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60") * 60
        });
    }

    /// <summary>
    /// Revoke a refresh token (logout).
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logout([FromBody] RefreshRequest request)
    {
        using var scope = _scopeFactory.CreateScope();
        var refreshTokenRepo = scope.ServiceProvider.GetRequiredService<IRepository<RefreshToken>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var token = await refreshTokenRepo.Query()
            .FirstOrDefaultAsync(t => t.Token == request.RefreshToken);

        if (token == null)
            return BadRequest(new { message = "Token not found." });

        token.RevokedAt = DateTime.UtcNow;
        token.RevokedByIp = GetIpAddress();
        refreshTokenRepo.Update(token);
        await unitOfWork.SaveChangesAsync();

        return Ok(new { message = "Token revoked successfully." });
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------

    private async Task<string> GenerateJwtToken(ApplicationUser user)
    {
        var roles = await _userManager.GetRolesAsync(user);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Email, user.Email!),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("firstName", user.FirstName),
            new("lastName", user.LastName)
        };

        foreach (var role in roles)
            claims.Add(new Claim(ClaimTypes.Role, role));

        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        // Internal DLD staff are capped at 30 minutes inactivity per RFP 10.2.1
        // Access Control. Public / investor users fall back to the configured
        // default (60 min) so an investor browsing the market isn't booted
        // mid-session.
        var defaultExpiry = int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");
        var internalExpiry = int.Parse(_configuration["Jwt:InternalExpiryMinutes"] ?? "30");
        var expiryMinutes = user.IsInternalUser ? internalExpiry : defaultExpiry;

        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(expiryMinutes),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private async Task<RefreshToken> CreateRefreshTokenAsync(string userId, string? ipAddress)
    {
        using var scope = _scopeFactory.CreateScope();
        var refreshTokenRepo = scope.ServiceProvider.GetRequiredService<IRepository<RefreshToken>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var refreshToken = new RefreshToken
        {
            UserId = userId,
            Token = GenerateRefreshTokenString(),
            ExpiresAt = DateTime.UtcNow.AddDays(int.Parse(_configuration["Jwt:RefreshTokenExpiryDays"] ?? "7")),
            CreatedByIp = ipAddress
        };

        await refreshTokenRepo.AddAsync(refreshToken);
        await unitOfWork.SaveChangesAsync();

        return refreshToken;
    }

    private static string GenerateRefreshTokenString()
    {
        var randomBytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(randomBytes);
        return Convert.ToBase64String(randomBytes);
    }

    private string? GetIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.MapToIPv4().ToString();
    }

    /// <summary>
    /// Builds the response body issued at the end of a successful login flow
    /// (with or without MFA).
    /// </summary>
    private async Task<IActionResult> IssueTokensAsync(ApplicationUser user)
    {
        user.LastLoginAt = DateTime.UtcNow;
        await _userManager.UpdateAsync(user);

        var accessToken = await GenerateJwtToken(user);
        var refreshToken = await CreateRefreshTokenAsync(user.Id, GetIpAddress());
        var roles = await _userManager.GetRolesAsync(user);
        var sessionMinutes = user.IsInternalUser
            ? int.Parse(_configuration["Jwt:InternalExpiryMinutes"] ?? "30")
            : int.Parse(_configuration["Jwt:ExpiryMinutes"] ?? "60");

        return Ok(new
        {
            accessToken,
            refreshToken = refreshToken.Token,
            expiresIn = sessionMinutes * 60,
            mfaEnabled = user.TwoFactorEnabled,
            user = new
            {
                id = user.Id,
                email = user.Email,
                firstName = user.FirstName,
                lastName = user.LastName,
                preferredLanguage = user.PreferredLanguage,
                isInternalUser = user.IsInternalUser,
                roles
            }
        });
    }

    /// <summary>
    /// Issues a short-lived (5-minute) JWT used between the password step and
    /// the MFA step. The "purpose" claim distinguishes enrolment from
    /// challenge tickets so the two endpoints cannot be cross-used.
    /// </summary>
    private string GenerateMfaTicket(string userId, string purpose)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _configuration["Jwt:Issuer"],
            audience: _configuration["Jwt:Audience"],
            claims: new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, userId),
                new Claim("mfa_purpose", purpose),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
            },
            expires: DateTime.UtcNow.AddMinutes(5),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private (string? UserId, string? Purpose) ValidateMfaTicket(string token)
    {
        try
        {
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]!));
            var validationParams = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = _configuration["Jwt:Issuer"],
                ValidateAudience = true,
                ValidAudience = _configuration["Jwt:Audience"],
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30)
            };
            var principal = new JwtSecurityTokenHandler().ValidateToken(token, validationParams, out _);
            var sub = principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
            var purpose = principal.FindFirstValue("mfa_purpose");
            return (sub, purpose);
        }
        catch
        {
            return (null, null);
        }
    }
}

// ---------------------------------------------------------------------------
// Request DTOs (kept alongside the controller for simplicity)
// ---------------------------------------------------------------------------

public class RegisterRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string? PreferredLanguage { get; set; }
}

public class LoginRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
}

public class RefreshRequest
{
    public string RefreshToken { get; set; } = default!;
}

public class LoginTwoFactorRequest
{
    public string TwoFactorToken { get; set; } = default!;
    public string Code { get; set; } = default!;
}

public class MfaSetupRequest
{
    public string MfaSetupToken { get; set; } = default!;
}

public class MfaEnableRequest
{
    public string MfaSetupToken { get; set; } = default!;
    public string Code { get; set; } = default!;
}
