using IRETP.Application.DTOs;
using IRETP.Domain.Enums;
using IRETP.Infrastructure.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace IRETP.AdminAPI.Controllers;

/// <summary>
/// RBAC user management (RFP Section 10.3). Restricted to DldSupervisor and
/// SystemAdministrator — operators cannot create or revoke accounts.
/// </summary>
[ApiController]
[Route("api/admin/users")]
[Authorize(Roles = $"{UserRoles.DldSupervisor},{UserRoles.SystemAdministrator}")]
public class UserAdminController : ControllerBase
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;

    public UserAdminController(
        UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager)
    {
        _userManager = userManager;
        _roleManager = roleManager;
    }

    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? role,
        [FromQuery] bool? internalOnly,
        CancellationToken ct = default)
    {
        var query = _userManager.Users.AsQueryable();
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(u =>
                (u.Email != null && u.Email.Contains(search)) ||
                (u.FirstName + " " + u.LastName).Contains(search));
        }
        if (internalOnly == true) query = query.Where(u => u.IsInternalUser);

        var users = await query.OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Take(200)
            .ToListAsync(ct);

        var result = new List<UserAdminDto>();
        foreach (var u in users)
        {
            var roles = await _userManager.GetRolesAsync(u);
            if (!string.IsNullOrWhiteSpace(role) && !roles.Contains(role, StringComparer.OrdinalIgnoreCase))
                continue;

            result.Add(new UserAdminDto
            {
                Id = u.Id,
                Email = u.Email,
                UserName = u.UserName,
                FirstName = u.FirstName,
                LastName = u.LastName,
                PreferredLanguage = u.PreferredLanguage,
                IsInternalUser = u.IsInternalUser,
                EmailConfirmed = u.EmailConfirmed,
                LockoutEnabled = u.LockoutEnabled,
                LockoutEnd = u.LockoutEnd?.UtcDateTime,
                LastLoginAt = u.LastLoginAt,
                Roles = roles.ToList()
            });
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = UserRoles.SystemAdministrator)]
    [ProducesResponseType(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest(new { error = "Email and password are required." });

        var user = new ApplicationUser
        {
            UserName = request.Email,
            Email = request.Email,
            FirstName = request.FirstName ?? "",
            LastName = request.LastName ?? "",
            IsInternalUser = request.IsInternalUser,
            PreferredLanguage = string.IsNullOrWhiteSpace(request.PreferredLanguage) ? "en" : request.PreferredLanguage!,
            EmailConfirmed = true
        };

        var created = await _userManager.CreateAsync(user, request.Password);
        if (!created.Succeeded)
            return BadRequest(new { errors = created.Errors.Select(e => e.Description) });

        if (request.Roles is { Count: > 0 })
        {
            foreach (var r in request.Roles.Distinct())
            {
                if (!await _roleManager.RoleExistsAsync(r)) continue;
                await _userManager.AddToRoleAsync(user, r);
            }
        }

        return CreatedAtAction(nameof(List), new { }, new { user.Id });
    }

    [HttpPut("{id}/roles")]
    [Authorize(Roles = UserRoles.SystemAdministrator)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> UpdateRoles(string id, [FromBody] UpdateRolesRequest request)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();

        var existing = await _userManager.GetRolesAsync(user);
        var toRemove = existing.Except(request.Roles ?? new List<string>()).ToList();
        var toAdd = (request.Roles ?? new List<string>()).Except(existing).ToList();

        if (toRemove.Count > 0) await _userManager.RemoveFromRolesAsync(user, toRemove);
        if (toAdd.Count > 0)
        {
            foreach (var r in toAdd)
            {
                if (await _roleManager.RoleExistsAsync(r))
                    await _userManager.AddToRoleAsync(user, r);
            }
        }
        return Ok(new { added = toAdd, removed = toRemove });
    }

    /// <summary>
    /// Disable a user's account — sets lockout end to the year 9999 which
    /// blocks every sign-in attempt until an administrator re-enables.
    /// RFP prohibits permanent deletions, so disable is the correct action.
    /// </summary>
    [HttpPut("{id}/disable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Disable(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        user.LockoutEnabled = true;
        await _userManager.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddYears(100));
        return Ok();
    }

    [HttpPut("{id}/enable")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Enable(string id)
    {
        var user = await _userManager.FindByIdAsync(id);
        if (user is null) return NotFound();
        await _userManager.SetLockoutEndDateAsync(user, null);
        return Ok();
    }
}

public class CreateUserRequest
{
    public string Email { get; set; } = default!;
    public string Password { get; set; } = default!;
    public string? FirstName { get; set; }
    public string? LastName { get; set; }
    public string? PreferredLanguage { get; set; }
    public bool IsInternalUser { get; set; } = true;
    public List<string>? Roles { get; set; }
}

public class UpdateRolesRequest
{
    public List<string>? Roles { get; set; }
}
