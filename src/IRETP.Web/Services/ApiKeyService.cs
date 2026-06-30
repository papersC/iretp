using System.Security.Cryptography;

namespace IRETP.Web.Services;

/// <summary>
/// Demo-mode API-key registry for the Open Data Developer Portal (Phase 1 #24).
/// Generates, stores, and revokes keys entirely in memory. Production persists
/// via the ApiKey entity managed by the backend.
/// </summary>
public class ApiKeyService
{
    public sealed record ApiKey(
        Guid Id,
        string Name,
        string Key,
        string OwnerEmail,
        string Tier,        // Free | Plus | Partner
        int RateLimitPerMinute,
        DateTime CreatedAt,
        DateTime? RevokedAt,
        int CallsLast30d,
        DateTime? LastUsedAt = null,
        DateTime? RotatedAt = null,
        string? PreviousKeyMasked = null);

    private readonly List<ApiKey> _keys;

    public event Action? OnChange;

    public ApiKeyService()
    {
        _keys =
        [
            new(Guid.NewGuid(), "Research Sandbox",  "iretp_live_7f3c8b92ad4e416fa5",
                "researcher@dubaidata.ae", "Free",    60,   new DateTime(2026, 3, 1),  null, 1843,
                LastUsedAt: new DateTime(2026, 4, 14, 9, 22, 0)),
            new(Guid.NewGuid(), "JLL Integration",   "iretp_live_3a918d2ec44b41c09b",
                "api@jll.ae",               "Partner", 600,  new DateTime(2026, 2, 10), null, 421_088,
                LastUsedAt: new DateTime(2026, 4, 15, 18, 44, 0)),
        ];
    }

    public IReadOnlyList<ApiKey> List() => _keys;

    public ApiKey Issue(string name, string email, string tier)
    {
        var raw = RandomNumberGenerator.GetBytes(16);
        var hex = Convert.ToHexString(raw).ToLowerInvariant();
        var tierLimits = tier switch
        {
            "Partner" => 600,
            "Plus"    => 240,
            _         => 60
        };
        var key = new ApiKey(Guid.NewGuid(), name, $"iretp_live_{hex}", email, tier, tierLimits,
            DateTime.UtcNow, null, 0);
        _keys.Insert(0, key);
        OnChange?.Invoke();
        return key;
    }

    public void Revoke(Guid id)
    {
        var idx = _keys.FindIndex(k => k.Id == id);
        if (idx < 0) return;
        _keys[idx] = _keys[idx] with { RevokedAt = DateTime.UtcNow };
        OnChange?.Invoke();
    }

    /// <summary>
    /// Rotate the secret value while preserving the key record (same id,
    /// name, tier, usage counters). Returns the new plaintext value so the
    /// UI can surface it in a "shown once" dialog — the old value is
    /// immediately invalidated.
    /// </summary>
    public ApiKey? Rotate(Guid id)
    {
        var idx = _keys.FindIndex(k => k.Id == id);
        if (idx < 0 || _keys[idx].RevokedAt.HasValue) return null;

        var prior = _keys[idx];
        var raw = RandomNumberGenerator.GetBytes(16);
        var hex = Convert.ToHexString(raw).ToLowerInvariant();
        var rotated = prior with
        {
            Key = $"iretp_live_{hex}",
            RotatedAt = DateTime.UtcNow,
            PreviousKeyMasked = MaskPartial(prior.Key)
        };
        _keys[idx] = rotated;
        OnChange?.Invoke();
        return rotated;
    }

    private static string MaskPartial(string key) =>
        key.Length <= 8 ? key : key[..8] + "…" + key[^4..];

    public static readonly IReadOnlyList<(string Name, string Method, string Path, string Description)> Endpoints = new[]
    {
        ("Transactions", "GET", "/api/v1/open-data/transactions", "Paginated sale/mortgage/gift/auction records with multi-dimensional filters."),
        ("Zones",        "GET", "/api/v1/open-data/zones",        "All Dubai zones with aggregated KPIs: transactions, avg price/sqft, rental yield."),
        ("Projects",     "GET", "/api/v1/open-data/projects",     "Registered projects with status, completion %, and developer linkage."),
        ("Price Index",  "GET", "/api/v1/open-data/price-index",  "Monthly/quarterly price-per-sqft index, filterable by zone and property type."),
        ("Rental Index", "GET", "/api/v1/open-data/rental-index", "Rental index + gross yield by zone and unit type."),
    };
}
