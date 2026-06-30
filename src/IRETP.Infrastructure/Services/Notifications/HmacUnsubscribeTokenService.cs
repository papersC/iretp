using System.Security.Cryptography;
using System.Text;
using IRETP.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IRETP.Infrastructure.Services.Notifications;

/// <summary>
/// HMAC-SHA256 unsubscribe token. Secret is pulled from
/// <c>Notifications:UnsubscribeSecret</c>; a process-wide fallback is used in
/// development so dev builds don't require the secret to be configured.
/// Tokens are stable for the life of the secret — rotating the secret
/// invalidates every outstanding unsubscribe link, which is the desired
/// behaviour after a compromise.
/// </summary>
public class HmacUnsubscribeTokenService : IUnsubscribeTokenService
{
    private readonly byte[] _key;

    public HmacUnsubscribeTokenService(IConfiguration configuration)
    {
        var secret = configuration["Notifications:UnsubscribeSecret"]
                     ?? configuration["UnsubscribeSecret"]
                     ?? "iretp-dev-unsubscribe-secret-please-rotate";
        _key = Encoding.UTF8.GetBytes(secret);
    }

    public string Mint(string userId, string reason)
    {
        var payload = $"{userId}|{reason}";
        using var hmac = new HMACSHA256(_key);
        var mac = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Base64UrlEncode(mac);
    }

    public bool Verify(string userId, string reason, string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        var expected = Mint(userId, reason);
        // Constant-time compare to avoid timing-oracle leaks.
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expected),
            Encoding.UTF8.GetBytes(token));
    }

    private static string Base64UrlEncode(byte[] bytes)
        => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
