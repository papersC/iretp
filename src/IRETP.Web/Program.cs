using System.Globalization;
using IRETP.Web.Components;
using IRETP.Web.Services;
using Microsoft.AspNetCore.Localization;

var builder = WebApplication.CreateBuilder(args);

// Razor + Blazor Server
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// HTTP clients for backend APIs
builder.Services.AddHttpClient("WebApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:WebApiUrl"] ?? "http://localhost:5000");
});
builder.Services.AddHttpClient("AdminApi", client =>
{
    client.BaseAddress = new Uri(builder.Configuration["ApiSettings:AdminApiUrl"] ?? "http://localhost:5002");
});

// Backend-client facades
builder.Services.AddScoped<WebApiClient>();
builder.Services.AddScoped<AdminApiClient>();

// Data / fixture services (singleton: same fixture across circuits)
builder.Services.AddSingleton<MarketDataService>();
builder.Services.AddSingleton<ProjectCatalogService>();
// Notifications are per-circuit (each user has their own feed); depends on
// scoped WebApiClient and AuthStateService.
builder.Services.AddScoped<NotificationFeedService>();
// Per-circuit (depends on scoped WebApiClient + AuthStateService)
builder.Services.AddScoped<WatchlistService>();
builder.Services.AddSingleton<CmsContentStore>();
builder.Services.AddSingleton<ApiKeyService>();
builder.Services.AddSingleton<SavedViewsService>();
builder.Services.AddSingleton<AdminFixtureService>();

// Scoped per-circuit services (MapDataService already existed, keep as-is)
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<MapDataService>();
builder.Services.AddScoped<UiStateService>();
builder.Services.AddScoped<AuthStateService>();
builder.Services.AddScoped<LocalizationService>();
builder.Services.AddScoped<CurrencyService>();
builder.Services.AddScoped<AiChatService>();
builder.Services.AddScoped<TransactionExportService>();
builder.Services.AddScoped<EscrowReportService>();
builder.Services.AddScoped<CaptchaGateService>();

// Localization — EN + AR (Phase 1) plus the Phase-4 extended set
// (Chinese, Russian, Urdu, French, Hindi, German — RFP Section 7).
builder.Services.AddLocalization();
builder.Services.Configure<RequestLocalizationOptions>(o =>
{
    var cultures = UiStateService.SupportedLanguages
        .Select(c => new CultureInfo(c))
        .ToArray();
    o.DefaultRequestCulture = new RequestCulture("en");
    o.SupportedCultures = cultures;
    o.SupportedUICultures = cultures;
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}

app.UseRequestLocalization();
app.UseAntiforgery();

app.MapStaticAssets();

// ---------------------------------------------------------------------------
// Sitemap — RFP Section 4 discoverability.
// Emits the static public routes plus every project's detail page so search
// engines can index the full catalogue. /robots.txt (wwwroot) references this.
// ---------------------------------------------------------------------------
app.MapGet("/sitemap.xml", (HttpContext ctx, ProjectCatalogService catalog) =>
{
    var baseUrl = $"{ctx.Request.Scheme}://{ctx.Request.Host.Value}";
    var today = DateTime.UtcNow.ToString("yyyy-MM-dd");

    // Public, crawlable routes. Admin/account/auth/personalisation pages are
    // deliberately excluded (they are already Disallow-ed in robots.txt).
    string[] staticPaths =
    {
        "/", "/transactions", "/projects", "/developers", "/map",
        "/price-index", "/rental-index", "/mortgage", "/benchmark",
        "/esg", "/greti", "/beneficial-ownership", "/ai-agent",
        "/api-portal"
    };

    var sb = new System.Text.StringBuilder();
    sb.Append("<?xml version=\"1.0\" encoding=\"UTF-8\"?>\n");
    sb.Append("<urlset xmlns=\"http://www.sitemaps.org/schemas/sitemap/0.9\">\n");

    foreach (var path in staticPaths)
    {
        var priority = path == "/" ? "1.0" : "0.8";
        sb.Append($"  <url><loc>{baseUrl}{path}</loc><lastmod>{today}</lastmod>")
          .Append($"<changefreq>daily</changefreq><priority>{priority}</priority></url>\n");
    }

    foreach (var project in catalog.All())
    {
        sb.Append($"  <url><loc>{baseUrl}/projects/{project.Id}</loc>")
          .Append($"<lastmod>{today}</lastmod><changefreq>weekly</changefreq><priority>0.6</priority></url>\n");
    }

    sb.Append("</urlset>\n");
    return Results.Content(sb.ToString(), "application/xml; charset=utf-8");
});

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
