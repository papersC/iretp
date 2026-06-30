namespace IRETP.Application.Interfaces;

/// <summary>
/// Server-side validator for the no-investment-advice constraint declared in
/// RFP Section 5.1. Even with the system prompt instructing the model not to
/// offer financial advice, leaked or jailbroken outputs must be caught and
/// replaced before they reach the user. Returns a non-null reason when the
/// answer looks like advisory output, in which case the caller substitutes
/// the canned refusal.
/// </summary>
public interface IAdvisoryGuardrail
{
    /// <summary>
    /// Returns null if the answer is acceptable, or a short violation reason
    /// (English, suitable for the audit log) if the response should be
    /// blocked.
    /// </summary>
    string? Validate(string answer);
}
