using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IRETP.Infrastructure.Services;

/// <summary>
/// Evaluates the active InvestorAlert configurations (RFP Section 6.1) and
/// emits Notification rows when a trigger condition is met. Supported triggers:
///   - PriceMovement — %Δ in latest PriceIndex for the configured zone exceeds
///     the user threshold (direction-aware).
///   - RentalYield — latest RentalIndex crosses the threshold in the direction
///     the user chose.
///   - NewProject — a Project was registered in the last 24h for the
///     configured zone / developer.
///   - WatchlistChange — a watched Project's completion % or delivery date
///     changed since the alert was last checked.
///   - MarketDigest — weekly / monthly periodic summary.
/// Runs hourly; idempotent via a per-alert LastEvaluatedAt marker stored as a
/// Notification row (providing a simple, schema-free high-water mark).
/// </summary>
public class InvestorAlertEvaluator
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<InvestorAlertEvaluator> _logger;

    public InvestorAlertEvaluator(IServiceScopeFactory scopeFactory, ILogger<InvestorAlertEvaluator> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task EvaluateAsync()
    {
        using var scope = _scopeFactory.CreateScope();
        var alertRepo = scope.ServiceProvider.GetRequiredService<IRepository<InvestorAlert>>();
        var priceRepo = scope.ServiceProvider.GetRequiredService<IRepository<PriceIndex>>();
        var rentalRepo = scope.ServiceProvider.GetRequiredService<IRepository<RentalIndex>>();
        var projectRepo = scope.ServiceProvider.GetRequiredService<IRepository<Project>>();
        var notificationRepo = scope.ServiceProvider.GetRequiredService<IRepository<Notification>>();
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

        var alerts = await alertRepo.Query()
            .Where(a => a.IsActive)
            .Include(a => a.Zone)
            .ToListAsync();

        if (alerts.Count == 0) return;

        var emitted = new List<Notification>();
        var now = DateTime.UtcNow;

        foreach (var alert in alerts)
        {
            var notifications = alert.AlertType switch
            {
                "PriceMovement" => await EvaluatePriceMovementAsync(alert, priceRepo, notificationRepo),
                "RentalYield" => await EvaluateRentalYieldAsync(alert, rentalRepo, notificationRepo),
                "NewProject" => await EvaluateNewProjectAsync(alert, projectRepo, notificationRepo, now),
                "WatchlistChange" => await EvaluateWatchlistAsync(alert, projectRepo, notificationRepo, now),
                "MarketDigest" => await EvaluateDigestAsync(alert, priceRepo, notificationRepo, now),
                _ => Array.Empty<Notification>()
            };

            emitted.AddRange(notifications);
        }

        if (emitted.Count > 0)
        {
            await notificationRepo.AddRangeAsync(emitted);
            await unitOfWork.SaveChangesAsync();
            _logger.LogInformation("InvestorAlertEvaluator emitted {Count} notification(s) from {AlertCount} active alert(s).",
                emitted.Count, alerts.Count);
        }
    }

    // -----------------------------------------------------------------------
    // Individual evaluators
    // -----------------------------------------------------------------------
    private static async Task<IReadOnlyList<Notification>> EvaluatePriceMovementAsync(
        InvestorAlert alert,
        IRepository<PriceIndex> priceRepo,
        IRepository<Notification> notificationRepo)
    {
        if (alert.ZoneId is null || alert.ThresholdValue is null) return [];

        var latest = await priceRepo.Query()
            .Where(p => p.ZoneId == alert.ZoneId)
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).ThenByDescending(p => p.Month)
            .FirstOrDefaultAsync();

        if (latest?.QuarterlyChange is null) return [];

        var change = latest.QuarterlyChange.Value; // percent
        var threshold = alert.ThresholdValue.Value;
        var direction = alert.ThresholdDirection ?? "Above";

        var triggered = string.Equals(direction, "Above", StringComparison.OrdinalIgnoreCase)
            ? change >= threshold
            : change <= -threshold;

        if (!triggered) return [];
        if (await AlreadyNotifiedRecentlyAsync(notificationRepo, alert.UserId, PriceLink(alert), TimeSpan.FromDays(1))) return [];

        var zoneName = alert.Zone?.Name ?? "the selected zone";
        var title = $"Price alert: {zoneName} {(change >= 0 ? "+" : "")}{change:F1}%";
        var titleAr = $"تنبيه أسعار: {zoneName} {(change >= 0 ? "+" : "")}{change:F1}%";
        var body = $"Average price per sqft in {zoneName} moved {change:F1}% quarter-over-quarter, crossing your {direction.ToLowerInvariant()} {threshold:F1}% threshold.";
        var bodyAr = $"تحرّك متوسط السعر للقدم المربع في {zoneName} بنسبة {change:F1}٪ مقارنة بالربع السابق، متجاوزاً العتبة المحددة.";

        return BuildRecipientChannels(alert, title, titleAr, body, bodyAr, "Price", PriceLink(alert));
    }

    private static async Task<IReadOnlyList<Notification>> EvaluateRentalYieldAsync(
        InvestorAlert alert,
        IRepository<RentalIndex> rentalRepo,
        IRepository<Notification> notificationRepo)
    {
        if (alert.ZoneId is null || alert.ThresholdValue is null) return [];

        var latest = await rentalRepo.Query()
            .Where(r => r.ZoneId == alert.ZoneId)
            .OrderByDescending(r => r.Year).ThenByDescending(r => r.Quarter)
            .FirstOrDefaultAsync();

        if (latest is null) return [];

        var threshold = alert.ThresholdValue.Value;
        var direction = alert.ThresholdDirection ?? "Above";
        var triggered = string.Equals(direction, "Above", StringComparison.OrdinalIgnoreCase)
            ? latest.GrossRentalYield >= threshold
            : latest.GrossRentalYield <= threshold;

        if (!triggered) return [];
        if (await AlreadyNotifiedRecentlyAsync(notificationRepo, alert.UserId, YieldLink(alert), TimeSpan.FromDays(7))) return [];

        var zoneName = alert.Zone?.Name ?? "the selected zone";
        var title = $"Rental yield alert: {zoneName} @ {latest.GrossRentalYield:F1}%";
        var titleAr = $"تنبيه العائد الإيجاري: {zoneName} @ {latest.GrossRentalYield:F1}٪";
        var body = $"Gross rental yield in {zoneName} is {latest.GrossRentalYield:F2}% — {direction.ToLowerInvariant()} your threshold of {threshold:F2}%.";
        var bodyAr = $"بلغ صافي العائد الإيجاري في {zoneName} {latest.GrossRentalYield:F2}٪ وتجاوز العتبة المحددة.";

        return BuildRecipientChannels(alert, title, titleAr, body, bodyAr, "Yield", YieldLink(alert));
    }

    private static async Task<IReadOnlyList<Notification>> EvaluateNewProjectAsync(
        InvestorAlert alert,
        IRepository<Project> projectRepo,
        IRepository<Notification> notificationRepo,
        DateTime now)
    {
        var since = now.AddHours(-25); // slight overlap vs hourly cadence to avoid misses
        var query = projectRepo.Query().Where(p => p.CreatedAt >= since);
        if (alert.ZoneId is not null) query = query.Where(p => p.ZoneId == alert.ZoneId);
        if (alert.DeveloperId is not null) query = query.Where(p => p.DeveloperId == alert.DeveloperId);

        var newProjects = await query
            .Include(p => p.Zone)
            .Include(p => p.Developer)
            .ToListAsync();

        if (newProjects.Count == 0) return [];

        var notes = new List<Notification>();
        foreach (var project in newProjects)
        {
            var link = $"/projects/{project.Id}";
            if (await AlreadyNotifiedRecentlyAsync(notificationRepo, alert.UserId, link, TimeSpan.FromDays(30))) continue;

            var title = $"New project launch: {project.Name}";
            var titleAr = $"إطلاق مشروع جديد: {project.NameAr}";
            var body = $"A new project by {project.Developer?.Name} has been registered in {project.Zone?.Name}.";
            var bodyAr = $"تم تسجيل مشروع جديد من {project.Developer?.Name} في {project.Zone?.Name}.";

            notes.AddRange(BuildRecipientChannels(alert, title, titleAr, body, bodyAr, "Project", link));
        }
        return notes;
    }

    private static async Task<IReadOnlyList<Notification>> EvaluateWatchlistAsync(
        InvestorAlert alert,
        IRepository<Project> projectRepo,
        IRepository<Notification> notificationRepo,
        DateTime now)
    {
        if (alert.ProjectId is null) return [];
        var project = await projectRepo.Query()
            .Include(p => p.Zone)
            .FirstOrDefaultAsync(p => p.Id == alert.ProjectId);
        if (project is null) return [];

        var since = now.AddHours(-25);
        var changedRecently = project.UpdatedAt >= since || project.CreatedAt >= since;
        if (!changedRecently) return [];

        var link = $"/projects/{project.Id}";
        if (await AlreadyNotifiedRecentlyAsync(notificationRepo, alert.UserId, link, TimeSpan.FromHours(23))) return [];

        var title = $"Watchlist update: {project.Name} — {project.CompletionPercentage:F0}%";
        var titleAr = $"تحديث قائمة المتابعة: {project.NameAr} — {project.CompletionPercentage:F0}٪";
        var body = $"A watched project has moved to {project.CompletionPercentage:F0}% completion with status {project.Status}.";
        var bodyAr = $"تقدّم مشروع خاضع للمتابعة إلى {project.CompletionPercentage:F0}٪ من الإنجاز.";

        return BuildRecipientChannels(alert, title, titleAr, body, bodyAr, "Watchlist", link);
    }

    private static async Task<IReadOnlyList<Notification>> EvaluateDigestAsync(
        InvestorAlert alert,
        IRepository<PriceIndex> priceRepo,
        IRepository<Notification> notificationRepo,
        DateTime now)
    {
        var cadence = (alert.Frequency ?? "Weekly").Equals("Monthly", StringComparison.OrdinalIgnoreCase)
            ? TimeSpan.FromDays(28)
            : TimeSpan.FromDays(7);

        var link = $"/analytics?digest={alert.Id}";
        if (await AlreadyNotifiedRecentlyAsync(notificationRepo, alert.UserId, link, cadence)) return [];

        var zoneIds = alert.ZoneId is null ? null : new[] { alert.ZoneId.Value };
        var query = priceRepo.Query();
        if (zoneIds is not null) query = query.Where(p => zoneIds.Contains(p.ZoneId));

        var latestByZone = await query
            .OrderByDescending(p => p.Year).ThenByDescending(p => p.Quarter).ThenByDescending(p => p.Month)
            .Take(10)
            .ToListAsync();

        if (latestByZone.Count == 0) return [];

        var averageChange = latestByZone
            .Where(p => p.QuarterlyChange.HasValue)
            .Select(p => p.QuarterlyChange!.Value)
            .DefaultIfEmpty(0m)
            .Average();

        var periodLabel = cadence.Days >= 28 ? "monthly" : "weekly";
        var zoneLabel = alert.Zone?.Name ?? "your zones of interest";

        var title = $"{Capitalize(periodLabel)} market digest: {zoneLabel}";
        var titleAr = periodLabel == "weekly" ? $"الموجز الأسبوعي للسوق: {zoneLabel}" : $"الموجز الشهري للسوق: {zoneLabel}";
        var body = $"Average quarterly change across {zoneLabel} is {averageChange:F1}% based on the latest DLD transactions.";
        var bodyAr = $"متوسط التغير الفصلي في {zoneLabel} هو {averageChange:F1}٪ استناداً إلى أحدث بيانات دائرة الأراضي.";

        return BuildRecipientChannels(alert, title, titleAr, body, bodyAr, "Digest", link);
    }

    // -----------------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------------
    private static IReadOnlyList<Notification> BuildRecipientChannels(
        InvestorAlert alert, string title, string titleAr, string body, string bodyAr, string category, string link)
    {
        var created = new List<Notification>
        {
            new()
            {
                UserId = alert.UserId,
                Title = title,
                TitleAr = titleAr,
                Message = body,
                MessageAr = bodyAr,
                Link = link,
                Channel = "InApp",
                Category = category,
                IsRead = false,
                IsSent = false
            }
        };

        if (alert.IsEmailEnabled)
        {
            created.Add(Clone(created[0], "Email"));
        }

        if (alert.IsSmsEnabled)
        {
            created.Add(Clone(created[0], "SMS"));
        }

        return created;
    }

    private static Notification Clone(Notification src, string channel) => new()
    {
        UserId = src.UserId,
        Title = src.Title,
        TitleAr = src.TitleAr,
        Message = src.Message,
        MessageAr = src.MessageAr,
        Link = src.Link,
        Channel = channel,
        Category = src.Category,
        IsRead = false,
        IsSent = false
    };

    private static string PriceLink(InvestorAlert alert) => $"/price-index?zone={alert.ZoneId}&alert={alert.Id}";
    private static string YieldLink(InvestorAlert alert) => $"/rental-index?zone={alert.ZoneId}&alert={alert.Id}";

    private static async Task<bool> AlreadyNotifiedRecentlyAsync(
        IRepository<Notification> repo, string userId, string link, TimeSpan window)
    {
        var cutoff = DateTime.UtcNow - window;
        return await repo.Query().AnyAsync(n =>
            n.UserId == userId && n.Link == link && n.CreatedAt >= cutoff);
    }

    private static string Capitalize(string value) =>
        string.IsNullOrEmpty(value) ? value : char.ToUpperInvariant(value[0]) + value[1..];
}
