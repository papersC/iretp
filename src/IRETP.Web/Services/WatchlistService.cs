using IRETP.Application.DTOs;
using Microsoft.Extensions.Logging;

namespace IRETP.Web.Services;

/// <summary>
/// Per-circuit watchlist + alert-configuration store for the public portal.
/// Primary source is the authenticated <c>/api/alerts/watchlist</c> endpoint
/// via <see cref="WebApiClient"/>; falls back to an illustrative fixture so
/// the demo UI still works when the user is anonymous.
/// </summary>
public class WatchlistService
{
    public sealed record WatchItem(
        Guid Id,
        string Kind,          // Project | Zone | Developer
        string Name,
        string? Detail,
        DateTime AddedAt,
        Guid? ProjectId = null,
        Guid? ZoneId = null,
        Guid? DeveloperId = null);

    public sealed record AlertConfig(
        Guid Id,
        string Type,          // PriceMovement | NewProject | WatchlistChange | RentalYield | MarketDigest | RegulationUpdate
        string Target,        // free-form label e.g. "Dubai Marina"
        decimal? Threshold,
        string? Direction,    // Above | Below
        string Frequency,     // Instant | Weekly | Monthly
        bool Email,
        bool Sms,
        bool InApp,
        bool Active);

    private readonly WebApiClient _api;
    private readonly AuthStateService _auth;
    private readonly ILogger<WatchlistService> _logger;
    private readonly List<WatchItem> _watches = new();
    private readonly List<AlertConfig> _alerts = new();
    private bool _loadedFromApi;

    public event Action? OnChange;

    public WatchlistService(WebApiClient api, AuthStateService auth, ILogger<WatchlistService> logger)
    {
        _api = api;
        _auth = auth;
        _logger = logger;
        SeedFixture();
    }

    // -----------------------------------------------------------------------
    // Watchlist
    // -----------------------------------------------------------------------
    public IReadOnlyList<WatchItem> Watches() => _watches;

    /// <summary>
    /// Refreshes the watchlist from the backend. No-op for anonymous users.
    /// </summary>
    public async Task<bool> RefreshAsync()
    {
        if (!_auth.IsAuthenticated) return false;

        try
        {
            var items = await _api.GetWatchlistAsync();
            if (items is null) return false;

            _watches.Clear();
            foreach (var item in items)
            {
                var kind = item.ProjectId.HasValue
                    ? "Project"
                    : item.ZoneId.HasValue ? "Zone" : "Developer";
                var name = item.ProjectName ?? item.ZoneName ?? item.DeveloperName ?? "—";
                _watches.Add(new WatchItem(
                    item.Id, kind, name, null, item.CreatedAt,
                    item.ProjectId, item.ZoneId, item.DeveloperId));
            }

            _loadedFromApi = true;
            OnChange?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Watchlist refresh failed; keeping local state.");
            return false;
        }
    }

    public async Task AddWatchAsync(string kind, string name, string? detail,
        Guid? projectId = null, Guid? zoneId = null, Guid? developerId = null)
    {
        Guid id;

        if (_loadedFromApi && _auth.IsAuthenticated && (projectId.HasValue || zoneId.HasValue || developerId.HasValue))
        {
            var persisted = await _api.AddWatchlistItemAsync(projectId, zoneId, developerId);
            id = persisted ?? Guid.NewGuid();
        }
        else
        {
            id = Guid.NewGuid();
        }

        _watches.Insert(0, new WatchItem(id, kind, name, detail, DateTime.UtcNow,
            projectId, zoneId, developerId));
        OnChange?.Invoke();
    }

    public async Task RemoveWatchAsync(Guid id)
    {
        if (_loadedFromApi && _auth.IsAuthenticated)
        {
            await _api.RemoveWatchlistItemAsync(id);
        }

        if (_watches.RemoveAll(w => w.Id == id) > 0)
        {
            OnChange?.Invoke();
        }
    }

    // Legacy sync overloads kept for existing razor code.
    public void AddWatch(string kind, string name, string? detail) =>
        _ = AddWatchAsync(kind, name, detail);

    public void RemoveWatch(Guid id) => _ = RemoveWatchAsync(id);

    // -----------------------------------------------------------------------
    // Alerts — API-backed when authenticated, fixture otherwise. Delegates
    // creation/deletion through the /api/alerts endpoints (the actual trigger
    // work is done by InvestorAlertEvaluator on the server).
    // -----------------------------------------------------------------------
    public IReadOnlyList<AlertConfig> Alerts() => _alerts;

    public async Task<bool> RefreshAlertsAsync()
    {
        if (!_auth.IsAuthenticated) return false;

        try
        {
            var items = await _api.GetAlertConfigurationsAsync();
            if (items is null) return false;

            _alerts.Clear();
            foreach (var item in items)
            {
                var target = item.ZoneName
                             ?? (item.ZoneId.HasValue ? item.ZoneId.Value.ToString("N")[..8] : null)
                             ?? (item.DeveloperId.HasValue ? "Developer" : null)
                             ?? (item.ProjectId.HasValue ? "Project" : null)
                             ?? "All Dubai";

                _alerts.Add(new AlertConfig(
                    item.Id,
                    item.AlertType,
                    target,
                    item.ThresholdValue,
                    item.ThresholdDirection,
                    item.Frequency ?? "Instant",
                    item.IsEmailEnabled,
                    item.IsSmsEnabled,
                    item.IsPushEnabled,
                    item.IsActive));
            }

            _loadedFromApi = true;
            OnChange?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Alert refresh failed; keeping local state.");
            return false;
        }
    }

    /// <summary>
    /// Persist a new alert configuration. When <paramref name="zoneId"/>
    /// / <paramref name="developerId"/> / <paramref name="projectId"/> are
    /// provided the alert is backed by a real database row; otherwise the
    /// entry is only held in local state (the evaluator requires GUIDs for
    /// PriceMovement / RentalYield triggers).
    /// </summary>
    public async Task AddAlertAsync(AlertConfig config,
        Guid? zoneId = null, Guid? developerId = null, Guid? projectId = null)
    {
        Guid id = config.Id;

        if (_auth.IsAuthenticated && _loadedFromApi)
        {
            var command = new
            {
                alertType = config.Type,
                zoneId,
                developerId,
                projectId,
                thresholdValue = config.Threshold,
                thresholdDirection = config.Direction,
                frequency = config.Frequency,
                isEmailEnabled = config.Email,
                isSmsEnabled = config.Sms,
                isPushEnabled = config.InApp
            };
            var persistedId = await _api.ConfigureAlertAsync(command);
            if (persistedId.HasValue) id = persistedId.Value;
        }

        _alerts.Insert(0, config with { Id = id });
        OnChange?.Invoke();
    }

    public async Task RemoveAlertAsync(Guid id)
    {
        if (_auth.IsAuthenticated && _loadedFromApi)
        {
            await _api.DeleteAlertAsync(id);
        }

        if (_alerts.RemoveAll(a => a.Id == id) > 0)
        {
            OnChange?.Invoke();
        }
    }

    // Legacy sync overloads kept for existing razor code.
    public void AddAlert(AlertConfig config) => _ = AddAlertAsync(config);
    public void RemoveAlert(Guid id) => _ = RemoveAlertAsync(id);

    public void ToggleAlert(Guid id)
    {
        var idx = _alerts.FindIndex(a => a.Id == id);
        if (idx < 0) return;
        _alerts[idx] = _alerts[idx] with { Active = !_alerts[idx].Active };
        OnChange?.Invoke();
    }

    public static readonly IReadOnlyList<string> AlertTypes = new[]
    {
        "PriceMovement", "NewProject", "WatchlistChange",
        "RentalYield",   "MarketDigest", "RegulationUpdate"
    };

    public static readonly IReadOnlyList<string> Frequencies = new[]
    {
        "Instant", "Daily", "Weekly", "Monthly"
    };

    // -----------------------------------------------------------------------
    // Fallback fixture (pre-auth / offline demo)
    // -----------------------------------------------------------------------
    private void SeedFixture()
    {
        var now = new DateTime(2026, 4, 10);
        _watches.AddRange(new[]
        {
            new WatchItem(Guid.NewGuid(), "Zone",      "Dubai Marina",      "Residential focus",  now.AddDays(-32)),
            new WatchItem(Guid.NewGuid(), "Project",   "Emaar Beachfront",  "Phase 3 — 62% done", now.AddDays(-20)),
            new WatchItem(Guid.NewGuid(), "Developer", "Emaar Properties",  "AAA rating",         now.AddDays(-15)),
        });
        _alerts.AddRange(new[]
        {
            new AlertConfig(Guid.NewGuid(), "PriceMovement", "Dubai Marina", 5m,   "Above", "Instant", true, true,  true, true),
            new AlertConfig(Guid.NewGuid(), "RentalYield",   "JVC",          7.5m, "Above", "Weekly",  true, false, true, true),
            new AlertConfig(Guid.NewGuid(), "NewProject",    "Emaar",        null, null,    "Instant", true, true,  true, true),
            new AlertConfig(Guid.NewGuid(), "MarketDigest",  "All Dubai",    null, null,    "Weekly",  true, false, true, true),
        });
    }
}
