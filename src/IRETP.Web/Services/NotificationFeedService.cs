using Microsoft.Extensions.Logging;

namespace IRETP.Web.Services;

/// <summary>
/// Per-circuit notification feed for the header bell and /notifications page.
/// Primary source is the backend <c>/api/alerts/notifications</c> endpoint
/// (wired through <see cref="WebApiClient"/>). When the user is unauthenticated
/// or the backend is unavailable the service falls back to a short illustrative
/// fixture so the demo UI still shows the six alert categories defined by
/// RFP Section 6.1.
/// </summary>
public class NotificationFeedService
{
    public sealed record Notification(
        Guid Id,
        string Title,
        string TitleAr,
        string Message,
        string MessageAr,
        string Channel,       // InApp | Email | SMS
        string Category,      // Price | Project | Watchlist | Yield | Digest | Regulation | Risk
        DateTime CreatedAt,
        bool IsRead,
        string? Link = null);

    private readonly WebApiClient _api;
    private readonly AuthStateService _auth;
    private readonly ILogger<NotificationFeedService> _logger;
    private readonly List<Notification> _items = new();
    private bool _loadedFromApi;

    public event Action? OnChange;

    public NotificationFeedService(WebApiClient api, AuthStateService auth, ILogger<NotificationFeedService> logger)
    {
        _api = api;
        _auth = auth;
        _logger = logger;
        SeedFixture();
    }

    public IReadOnlyList<Notification> List() => _items;

    public int UnreadCount() => _items.Count(n => !n.IsRead);

    /// <summary>
    /// Refreshes the feed from the backend API if the current user is
    /// authenticated. No-op (and no error) when the user is anonymous — the
    /// seeded fixture continues to back the UI.
    /// </summary>
    public async Task<bool> RefreshAsync()
    {
        if (!_auth.IsAuthenticated) return false;

        try
        {
            var page = await _api.GetNotificationsAsync(pageSize: 100);
            if (page is null) return false;

            _items.Clear();
            foreach (var item in page.Items)
            {
                _items.Add(new Notification(
                    item.Id,
                    item.Title,
                    string.IsNullOrEmpty(item.TitleAr) ? item.Title : item.TitleAr,
                    item.Message,
                    string.IsNullOrEmpty(item.MessageAr) ? item.Message : item.MessageAr,
                    item.Channel,
                    NormaliseCategory(item.Category),
                    item.CreatedAt,
                    item.IsRead,
                    item.Link));
            }
            _loadedFromApi = true;
            OnChange?.Invoke();
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to refresh notifications from API; keeping current in-memory state.");
            return false;
        }
    }

    public async Task MarkReadAsync(Guid id)
    {
        var idx = _items.FindIndex(n => n.Id == id);
        if (idx < 0) return;
        if (_items[idx].IsRead) return;

        if (_loadedFromApi && _auth.IsAuthenticated)
        {
            await _api.MarkNotificationReadAsync(id);
        }

        _items[idx] = _items[idx] with { IsRead = true };
        OnChange?.Invoke();
    }

    public async Task MarkAllReadAsync()
    {
        if (_loadedFromApi && _auth.IsAuthenticated)
        {
            await _api.MarkAllNotificationsReadAsync();
        }

        for (var i = 0; i < _items.Count; i++)
        {
            if (!_items[i].IsRead) _items[i] = _items[i] with { IsRead = true };
        }
        OnChange?.Invoke();
    }

    /// <summary>
    /// Legacy synchronous mark-read used by existing UI — kept for backwards
    /// compatibility. Delegates to <see cref="MarkReadAsync"/> without
    /// awaiting; the in-memory mutation still happens immediately.
    /// </summary>
    public void MarkRead(Guid id) => _ = MarkReadAsync(id);

    public void MarkAllRead() => _ = MarkAllReadAsync();

    /// <summary>
    /// Used by AI agent demo flows to inject a notification optimistically.
    /// API-backed refreshes will overwrite these on next poll.
    /// </summary>
    public void Push(string title, string titleAr, string message, string messageAr,
                     string channel, string category, string? link = null)
    {
        _items.Insert(0, new Notification(Guid.NewGuid(), title, titleAr, message, messageAr,
            channel, category, DateTime.UtcNow, false, link));
        OnChange?.Invoke();
    }

    // -----------------------------------------------------------------------
    // Fallback fixture (pre-auth / offline demo)
    // -----------------------------------------------------------------------
    private void SeedFixture()
    {
        var now = DateTime.UtcNow;
        _items.AddRange(new[]
        {
            new Notification(Guid.NewGuid(),
                "Price alert: Dubai Marina +6.2%",
                "تنبيه أسعار: دبي مارينا +٦٫٢٪",
                "Average price per sqft in Dubai Marina rose 6.2% vs. the 30-day baseline.",
                "ارتفع متوسط السعر للقدم المربع في دبي مارينا بنسبة ٦٫٢٪ مقارنة بمعيار الـ٣٠ يومًا.",
                "InApp", "Price", now.AddHours(-1), false, "/price-index"),
            new Notification(Guid.NewGuid(),
                "New project launch: Emaar Beachfront Phase 4",
                "إطلاق مشروع جديد: إعمار بيتشفرونت المرحلة ٤",
                "A new off-plan project by Emaar has been registered in Dubai Harbour.",
                "تم تسجيل مشروع على الخارطة جديد من إعمار في ميناء دبي.",
                "InApp", "Project", now.AddHours(-5), false, "/projects"),
            new Notification(Guid.NewGuid(),
                "Watchlist update: Creek Horizon completion 78%",
                "تحديث قائمة المتابعة: كريك هورايزن ٧٨٪",
                "A watched project has moved from 71% to 78% completion.",
                "انتقل أحد المشاريع الخاضعة للمتابعة من ٧١٪ إلى ٧٨٪ من الإنجاز.",
                "Email", "Watchlist", now.AddHours(-18), true),
            new Notification(Guid.NewGuid(),
                "Rental yield threshold crossed in JVC",
                "تجاوز عتبة العائد الإيجاري في قرية جميرا الدائرية",
                "Gross rental yield in Jumeirah Village Circle exceeded 7.5% this week.",
                "تجاوز صافي العائد الإيجاري في قرية جميرا الدائرية ٧٫٥٪ هذا الأسبوع.",
                "InApp", "Yield", now.AddDays(-1), true, "/rental-index"),
            new Notification(Guid.NewGuid(),
                "Weekly market digest available",
                "الملخص الأسبوعي للسوق متاح",
                "Your Dubai market summary for the past 7 days is ready.",
                "ملخص سوق دبي لآخر ٧ أيام جاهز.",
                "Email", "Digest", now.AddDays(-2), true, "/analytics"),
            new Notification(Guid.NewGuid(),
                "RERA regulation update",
                "تحديث لائحة تنظيم العقار",
                "Amended Escrow release mechanics affecting off-plan phase-3 milestones.",
                "تعديل على آلية صرف حساب الضمان تؤثر على المراحل الثالثة من المشاريع على الخارطة.",
                "InApp", "Regulation", now.AddDays(-3), true),
        });
    }

    private static string NormaliseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category)) return "Risk";
        return category;
    }
}
