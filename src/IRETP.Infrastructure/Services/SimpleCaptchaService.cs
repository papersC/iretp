using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using IRETP.Application.Interfaces;
using Microsoft.Extensions.Configuration;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// In-memory CAPTCHA implementation. Generates a short arithmetic challenge
/// (two single-digit integers summed) and issues an HMAC-signed token on
/// correct answer. Keeps a bounded set of recent challenges and tokens so the
/// service has no external dependencies. Suitable for the public portal's
/// Export flow; DLD can swap in reCAPTCHA / hCaptcha later by replacing the
/// implementation behind <see cref="ICaptchaService"/>.
/// </summary>
public class SimpleCaptchaService : ICaptchaService
{
    private const int ChallengeCap = 500;
    private const int TokenCap = 1000;
    private static readonly TimeSpan ChallengeTtl = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan TokenTtl = TimeSpan.FromMinutes(30);

    private readonly ConcurrentDictionary<string, (int Answer, DateTime ExpiresAt)> _challenges = new();
    private readonly ConcurrentDictionary<string, DateTime> _tokens = new();
    private readonly byte[] _secret;

    public SimpleCaptchaService(IConfiguration configuration)
    {
        var secret = configuration["Captcha:SigningSecret"]
                     ?? "iretp-captcha-default-secret-change-in-production";
        _secret = Encoding.UTF8.GetBytes(secret);
    }

    public CaptchaChallenge CreateChallenge()
    {
        PurgeExpired();

        var rng = RandomNumberGenerator.GetInt32(2, 10);
        var rhs = RandomNumberGenerator.GetInt32(2, 10);
        var id = Guid.NewGuid().ToString("N");
        var expires = DateTime.UtcNow + ChallengeTtl;

        _challenges[id] = (rng + rhs, expires);
        BoundDictionary(_challenges, ChallengeCap);

        return new CaptchaChallenge(id, $"What is {rng} + {rhs}?", expires);
    }

    public string? VerifyAnswer(string challengeId, string answer)
    {
        PurgeExpired();

        if (!_challenges.TryRemove(challengeId, out var challenge)) return null;
        if (DateTime.UtcNow > challenge.ExpiresAt) return null;
        if (!int.TryParse(answer.Trim(), out var submitted) || submitted != challenge.Answer) return null;

        var expiresAt = DateTime.UtcNow + TokenTtl;
        var token = IssueToken(expiresAt);
        _tokens[token] = expiresAt;
        BoundDictionary(_tokens, TokenCap);
        return token;
    }

    public bool ValidateToken(string token)
    {
        if (string.IsNullOrWhiteSpace(token)) return false;
        PurgeExpired();

        if (!_tokens.TryGetValue(token, out var expiresAt)) return false;
        if (DateTime.UtcNow > expiresAt)
        {
            _tokens.TryRemove(token, out _);
            return false;
        }

        // Double-check HMAC so tokens from a previous process restart can't
        // be replayed once the in-memory table has been cleared.
        return VerifyTokenSignature(token, expiresAt);
    }

    private string IssueToken(DateTime expiresAt)
    {
        var nonce = Convert.ToBase64String(RandomNumberGenerator.GetBytes(12));
        var body = $"{expiresAt:O}|{nonce}";
        var signature = Convert.ToBase64String(HmacSha256(body));
        return Convert.ToBase64String(Encoding.UTF8.GetBytes($"{body}|{signature}"));
    }

    private bool VerifyTokenSignature(string token, DateTime expiresAt)
    {
        try
        {
            var raw = Encoding.UTF8.GetString(Convert.FromBase64String(token));
            var parts = raw.Split('|');
            if (parts.Length != 3) return false;
            if (!DateTime.TryParse(parts[0], null, System.Globalization.DateTimeStyles.RoundtripKind, out var storedExpiry)) return false;
            if (storedExpiry != expiresAt) return false;
            var expected = Convert.ToBase64String(HmacSha256($"{parts[0]}|{parts[1]}"));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(parts[2]));
        }
        catch
        {
            return false;
        }
    }

    private byte[] HmacSha256(string message)
    {
        using var hmac = new HMACSHA256(_secret);
        return hmac.ComputeHash(Encoding.UTF8.GetBytes(message));
    }

    private void PurgeExpired()
    {
        var now = DateTime.UtcNow;
        foreach (var kv in _challenges)
        {
            if (kv.Value.ExpiresAt < now) _challenges.TryRemove(kv.Key, out _);
        }
        foreach (var kv in _tokens)
        {
            if (kv.Value < now) _tokens.TryRemove(kv.Key, out _);
        }
    }

    private static void BoundDictionary<TKey, TValue>(ConcurrentDictionary<TKey, TValue> dict, int cap)
        where TKey : notnull
    {
        if (dict.Count <= cap) return;
        foreach (var key in dict.Keys.Take(dict.Count - cap).ToList())
        {
            dict.TryRemove(key, out _);
        }
    }
}
