namespace IRETP.Application.Interfaces;

/// <summary>
/// Thin shim that lets framework-agnostic Application/Infrastructure
/// code query the authenticated user's PDPL consent flags without
/// depending on ASP.NET Identity directly (RFP §19.2 + AI-006).
/// </summary>
public interface IUserConsent
{
    /// <summary>
    /// Returns <c>true</c> when the user has explicitly opted into
    /// cross-session AI memory. Null / unknown users always return false.
    /// </summary>
    Task<bool> HasAiMemoryConsentAsync(string? userId, CancellationToken ct = default);
}
