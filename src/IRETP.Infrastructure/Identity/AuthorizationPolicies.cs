using IRETP.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace IRETP.Infrastructure.Identity;

/// <summary>
/// RBAC policy catalog from RFP Section 10.3. Both APIs register these policy
/// names so controllers can simply declare <c>[Authorize(Policy = ...)]</c>
/// without each project repeating the role-set behind it.
/// </summary>
public static class AuthorizationPolicies
{
    // --- Internal-platform policies -----------------------------------------
    public const string InternalRead   = "internal.read";    // Viewer + above
    public const string InternalEdit   = "internal.edit";    // Operator + above
    public const string InternalManage = "internal.manage";  // Supervisor + above
    public const string SystemAdmin    = "internal.admin";   // SystemAdministrator only

    // --- External-platform policies -----------------------------------------
    public const string Investor       = "external.investor"; // RegisteredInvestor + above

    /// <summary>
    /// Registers the policy catalog on the supplied <see cref="AuthorizationOptions"/>.
    /// Policies are cumulative — a Supervisor satisfies InternalRead, InternalEdit,
    /// and InternalManage. Only the SystemAdministrator role satisfies SystemAdmin.
    /// </summary>
    public static void Register(AuthorizationOptions options)
    {
        options.AddPolicy(InternalRead, p => p.RequireRole(
            UserRoles.DldViewer,
            UserRoles.DldOperator,
            UserRoles.DldSupervisor,
            UserRoles.SystemAdministrator));

        options.AddPolicy(InternalEdit, p => p.RequireRole(
            UserRoles.DldOperator,
            UserRoles.DldSupervisor,
            UserRoles.SystemAdministrator));

        options.AddPolicy(InternalManage, p => p.RequireRole(
            UserRoles.DldSupervisor,
            UserRoles.SystemAdministrator));

        options.AddPolicy(SystemAdmin, p => p.RequireRole(
            UserRoles.SystemAdministrator));

        options.AddPolicy(Investor, p => p.RequireRole(
            UserRoles.RegisteredInvestor,
            UserRoles.DldViewer,
            UserRoles.DldOperator,
            UserRoles.DldSupervisor,
            UserRoles.SystemAdministrator));
    }
}
