using Microsoft.AspNetCore.Identity;

namespace IRETP.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string FirstName { get; set; } = default!;
    public string LastName { get; set; } = default!;
    public string PreferredLanguage { get; set; } = "en";
    public string PreferredCurrency { get; set; } = "AED";
    public bool IsInternalUser { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // -----------------------------------------------------------------------
    // PDPL consent flags (RFP Section 19.2 — Personal Data Protection
    // Compliance). All default to false so users must opt in explicitly.
    // -----------------------------------------------------------------------

    /// <summary>Consent for marketing/periodic-digest emails.</summary>
    public bool ConsentMarketing { get; set; }

    /// <summary>
    /// Cross-session AI Agent memory opt-in (RFP AI006). When false the
    /// Agent discards memory at session end.
    /// </summary>
    public bool ConsentAiMemory { get; set; }

    /// <summary>Consent for usage analytics on the portal.</summary>
    public bool ConsentUsageAnalytics { get; set; }

    public DateTime? ConsentUpdatedAt { get; set; }
}
