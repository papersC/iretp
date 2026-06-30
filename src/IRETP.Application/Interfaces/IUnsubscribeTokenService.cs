namespace IRETP.Application.Interfaces;

/// <summary>
/// Mints and verifies one-click unsubscribe tokens (RFP §6.2 + RFC 8058).
/// Tokens are keyed to (userId, reason) so a single email's Unsubscribe
/// link can't be re-used to opt out of a channel the user didn't ask about.
/// </summary>
public interface IUnsubscribeTokenService
{
    /// <summary>
    /// Returns an opaque token a browser or mail client can POST back to
    /// <c>/api/account/unsubscribe</c> to revoke the given subscription.
    /// </summary>
    string Mint(string userId, string reason);

    /// <summary>
    /// Verifies the token. Returns true when the signature matches and the
    /// claimed userId matches the supplied one.
    /// </summary>
    bool Verify(string userId, string reason, string token);
}
