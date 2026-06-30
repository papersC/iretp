namespace IRETP.Web.Services;

/// <summary>
/// Session auth state read from a cookie on circuit boot, updated when the
/// user logs in or logs out. Backends (<see cref="WebApiClient"/> and
/// <see cref="AdminApiClient"/>) call <see cref="SetToken(string, string, string?)"/>
/// after a successful login to pin the bearer token for the circuit.
/// </summary>
public class AuthStateService
{
    public const string CookieKey = "iretp.session";

    public string? AccessToken { get; private set; }
    public string? Email { get; private set; }
    public string? DisplayName { get; private set; }
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
    public bool IsInternalUser { get; private set; }

    public event Action? OnChange;

    public AuthStateService(IHttpContextAccessor? http = null,
        WebApiClient? web = null, AdminApiClient? admin = null)
    {
        // Hydrate from cookie on construction (circuit start).
        var ctx = http?.HttpContext;
        if (ctx == null) return;
        if (!ctx.Request.Cookies.TryGetValue(CookieKey, out var raw) || string.IsNullOrEmpty(raw))
            return;

        var parts = Decode(raw);
        if (parts.Count < 2) return;

        AccessToken = parts[0];
        Email = parts[1];
        DisplayName = parts.Count > 2 ? parts[2] : Email;
        web?.SetToken(AccessToken);
        admin?.SetToken(AccessToken);
    }

    public void SetToken(string token, string email, string? displayName = null, bool isInternalUser = false)
    {
        AccessToken = token;
        Email = email;
        DisplayName = displayName ?? email;
        IsInternalUser = isInternalUser;
        OnChange?.Invoke();
    }

    public void Clear()
    {
        AccessToken = null;
        Email = null;
        DisplayName = null;
        IsInternalUser = false;
        OnChange?.Invoke();
    }

    public string Encode() => string.Join("\u0001", new[] { AccessToken ?? "", Email ?? "", DisplayName ?? "" });

    private static List<string> Decode(string raw)
    {
        try
        {
            return raw.Split('\u0001').ToList();
        }
        catch
        {
            return new();
        }
    }
}
