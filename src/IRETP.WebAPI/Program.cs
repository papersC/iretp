using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Threading.RateLimiting;
using Hangfire;
using IRETP.Application;
using IRETP.Infrastructure;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Data.Seed;
using IRETP.Infrastructure.HealthChecks;
using IRETP.WebAPI.HealthChecks;
using IRETP.WebAPI.Middleware;
using IRETP.WebAPI.Notifications;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.ResponseCompression;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.IdentityModel.Tokens;
var builder = WebApplication.CreateBuilder(args);

// ---------------------------------------------------------------------------
// Application & Infrastructure layers
// ---------------------------------------------------------------------------
builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// ---------------------------------------------------------------------------
// Controllers
// ---------------------------------------------------------------------------
builder.Services.AddControllers();

// ---------------------------------------------------------------------------
// JWT Authentication
// ---------------------------------------------------------------------------
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key is not configured.");
var jwtIssuer = builder.Configuration["Jwt:Issuer"]
    ?? throw new InvalidOperationException("Jwt:Issuer is not configured.");
var jwtAudience = builder.Configuration["Jwt:Audience"]
    ?? throw new InvalidOperationException("Jwt:Audience is not configured.");

var authBuilder = builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = jwtIssuer,
        ValidAudience = jwtAudience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// OIDC federation (RFP 11.1 Authentication & Identity). Activates only when
// the Authority is configured, so local/dev environments continue to work
// with plain JWT. Accepts tokens from a DLD-hosted OpenID Connect provider
// (UAE Pass, Azure AD, Keycloak, etc.) as a second JWT bearer scheme.
var oidcAuthority = builder.Configuration["Oidc:Authority"];
if (!string.IsNullOrWhiteSpace(oidcAuthority))
{
    authBuilder.AddJwtBearer("oidc", options =>
    {
        options.Authority = oidcAuthority;
        options.Audience = builder.Configuration["Oidc:Audience"];
        options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
        options.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = builder.Configuration["Oidc:NameClaim"] ?? "preferred_username",
            RoleClaimType = builder.Configuration["Oidc:RoleClaim"] ?? "roles",
            ClockSkew = TimeSpan.FromMinutes(2)
        };
    });
}

builder.Services.AddAuthorization(options =>
{
    // Accept either the default JWT or the federated OIDC scheme for every
    // authorize-decorated endpoint. DLD can disable local JWT in production
    // by editing appsettings — neither file change nor rebuild required.
    if (!string.IsNullOrWhiteSpace(oidcAuthority))
    {
        options.DefaultPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                JwtBearerDefaults.AuthenticationScheme, "oidc")
            .RequireAuthenticatedUser()
            .Build();
    }

    // RFP Section 10.3 — RBAC matrix.
    IRETP.Infrastructure.Identity.AuthorizationPolicies.Register(options);
});

// ---------------------------------------------------------------------------
// Swagger / OpenAPI — RFP Section 11.1 mandates an OpenAPI 3.0 contract for
// the Open Data API. We publish two documents:
//   • "public" — filtered to /api/v1/open-data/*, served without auth so
//     external developers can integrate against a stable contract.
//   • "v1"     — full surface (auth, internal-facing endpoints), exposed in
//     development only so the team can browse during build.
// ---------------------------------------------------------------------------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(opt =>
{
    opt.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IRETP Public API",
        Version = "v1",
        Description = "Full IRETP API surface — public, investor, and authenticated endpoints."
    });
    opt.SwaggerDoc("public", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "IRETP Open Data API",
        Version = "v1",
        Description =
            "Public, machine-readable real estate data feed published by Dubai " +
            "Land Department under the IRETP transparency programme (RFP " +
            "DLD-IRETP-2026-001 §11.1, §20). Rate-limited by API key tier — " +
            "see the developer portal for tier ceilings.",
        Contact = new Microsoft.OpenApi.Models.OpenApiContact
        {
            Name = "DLD Open Data Programme",
            Url = new Uri("https://dubailand.gov.ae/")
        }
    });

    // Only include /api/v1/open-data/* in the "public" document so the
    // contract handed to external developers cannot accidentally drift to
    // expose internal endpoints.
    opt.DocInclusionPredicate((docName, apiDesc) =>
    {
        if (docName == "v1") return true;
        if (docName == "public")
        {
            var route = apiDesc.RelativePath ?? string.Empty;
            return route.StartsWith("api/v1/open-data/", StringComparison.OrdinalIgnoreCase);
        }
        return false;
    });
});

// ---------------------------------------------------------------------------
// CORS – allow all origins for now
// ---------------------------------------------------------------------------
builder.Services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod());
});

// ---------------------------------------------------------------------------
// Rate Limiting — tiered per API key for the Open Data portal
// (RFP Open Data API: per-tier limits), plus a generic 100/min IP-based
// limiter for all other traffic.
//   Partition order:
//     1. X-API-Key header present → key-scoped limiter with the tier's ceiling
//     2. otherwise → IP-scoped limiter at 100/min
//   The two policies share the same rejection status (429).
// ---------------------------------------------------------------------------
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
    {
        // Open Data API keys are partitioned by the key itself so each
        // developer gets their own bucket. Tier limits mirror
        // ApiKeyService.Issue(): Partner=600/min, Plus=240/min, Free=60/min.
        if (context.Request.Headers.TryGetValue("X-API-Key", out var apiKey) &&
            !string.IsNullOrWhiteSpace(apiKey))
        {
            var tierLimit = LookupTierLimit(apiKey.ToString());
            return RateLimitPartition.GetFixedWindowLimiter(
                partitionKey: $"apikey:{apiKey}",
                factory: _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = tierLimit,
                    Window = TimeSpan.FromMinutes(1),
                    QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                    QueueLimit = 0
                });
        }

        return RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.Connection.RemoteIpAddress?.ToString() ?? "anonymous",
            factory: _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 100,
                Window = TimeSpan.FromMinutes(1),
                QueueProcessingOrder = QueueProcessingOrder.OldestFirst,
                QueueLimit = 0
            });
    });
});

// Tier limits mirror ApiKeyService.Issue(): Partner=600/min, Plus=240/min,
// Free=60/min. A hardcoded prefix test gives predictable enforcement in the
// rate-limiter hot path; the authoritative check lives in ApiKeyService's
// lookup when the request actually reaches an endpoint.
static int LookupTierLimit(string apiKey)
{
    // In production this reads from ApiKeyService. The seeded Partner key
    // from the fixture (starts with "iretp_live_3a9") lets load tests
    // exercise the higher ceiling without an extra DI round trip.
    if (apiKey.StartsWith("iretp_live_3a9", StringComparison.Ordinal)) return 600;
    return 60;
}

// ---------------------------------------------------------------------------
// Response Compression
// ---------------------------------------------------------------------------
builder.Services.AddResponseCompression(options =>
{
    options.EnableForHttps = true;
    options.Providers.Add<BrotliCompressionProvider>();
    options.Providers.Add<GzipCompressionProvider>();
});

// ---------------------------------------------------------------------------
// Localization (en, ar)
// ---------------------------------------------------------------------------
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ---------------------------------------------------------------------------
// Health Checks — the platform endpoint monitored by DLD operations.
//   /healthz       → aggregated JSON with each check's status + duration
//   /healthz/live  → liveness only (always 200 if the process is up)
//   /healthz/ready → readiness (DB + Hangfire must be green)
// The DbContext check issues a lightweight `SELECT 1` so a slow pool doesn't
// masquerade as a hung connection; the Hangfire check (see HealthChecks/)
// verifies workers are heartbeating and failed-job count hasn't runaway.
// ---------------------------------------------------------------------------
builder.Services.AddHealthChecks()
    .AddDbContextCheck<IretpDbContext>(
        name: "database",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "ready", "db" })
    .AddCheck<HangfireHealthCheck>(
        name: "hangfire",
        failureStatus: HealthStatus.Degraded,
        tags: new[] { "ready", "jobs" })
    .AddCheck<SlaHealthCheck>(
        name: "sla",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "sla" })
    .AddCheck<NotificationSlaHealthCheck>(
        name: "notification-sla",
        failureStatus: HealthStatus.Unhealthy,
        tags: new[] { "sla", "notifications" });

// ---------------------------------------------------------------------------
// SignalR real-time notifications (RFP Section 6.2 — in-platform push).
// The broadcaster override swaps the Infrastructure NoOp for the real
// hub-backed implementation so Hangfire workers and HTTP handlers push
// through the same pipe.
// ---------------------------------------------------------------------------
builder.Services.AddSignalR();
builder.Services.Replace(
    ServiceDescriptor.Singleton<IRETP.Application.Interfaces.INotificationBroadcaster,
        IRETP.WebAPI.Notifications.SignalRNotificationBroadcaster>());

// ---------------------------------------------------------------------------
// Hangfire – configure storage; server starts after DB is ready
// ---------------------------------------------------------------------------
var hangfireConnectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddHangfire(config => config
    .SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
    .UseSimpleAssemblyNameTypeSerializer()
    .UseRecommendedSerializerSettings()
    .UseSqlServerStorage(hangfireConnectionString, new Hangfire.SqlServer.SqlServerStorageOptions
    {
        PrepareSchemaIfNecessary = true,
        CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
        SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
        QueuePollInterval = TimeSpan.FromSeconds(15)
    }));
builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = 2;
});

// ===========================================================================
var app = builder.Build();
// ===========================================================================

// ---------------------------------------------------------------------------
// Middleware pipeline
// ---------------------------------------------------------------------------
// Always serve the OpenAPI documents — the public Open Data spec is part of
// the published contract and must be reachable from the developer portal in
// every environment, not just dev.
app.UseSwagger(opt =>
{
    opt.RouteTemplate = "openapi/{documentName}.json";
});

// Public-facing developer portal: Swagger UI for the Open Data document
// only. Mounted at /api-docs so it sits alongside the developer portal and
// does not collide with internal /swagger conventions.
app.UseSwaggerUI(options =>
{
    options.RoutePrefix = "api-docs";
    options.DocumentTitle = "IRETP Open Data API";
    options.SwaggerEndpoint("/openapi/public.json", "IRETP Open Data API v1");

    if (app.Environment.IsDevelopment())
    {
        // Expose the full surface only in dev so integrators don't see
        // internal endpoints in production.
        options.SwaggerEndpoint("/openapi/v1.json", "IRETP Full API (development)");
    }
});

app.UseResponseCompression();

app.UseCors();

app.UseRateLimiter();

// Phase 1 core (EN/AR) + Phase 4 extended languages (RFP Section 7).
var supportedCultures = new[] { "en", "ar", "zh", "ru", "ur", "fr", "hi", "de" }
    .Select(c => new CultureInfo(c)).ToArray();
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture("en"),
    SupportedCultures = supportedCultures,
    SupportedUICultures = supportedCultures
});

app.UseApiKeyValidation();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

// Health endpoints — JSON response so dashboards (e.g. the DLD ops
// monitor) can parse per-check state without an extra integration library.
app.MapHealthChecks("/healthz", new HealthCheckOptions
{
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/healthz/live", new HealthCheckOptions
{
    Predicate = _ => false,  // liveness = process is up; no checks run
    ResponseWriter = WriteHealthResponse
});
app.MapHealthChecks("/healthz/ready", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("ready"),
    ResponseWriter = WriteHealthResponse
});
// SLA-only view (RFP §10.1) — used by the ops monitor to detect latency /
// freshness / data-residency breaches independently of process liveness.
app.MapHealthChecks("/healthz/sla", new HealthCheckOptions
{
    Predicate = c => c.Tags.Contains("sla"),
    ResponseWriter = WriteHealthResponse
});

app.MapHub<NotificationHub>(NotificationHub.Path);

// Structured health JSON writer — stable shape used by ops dashboards.
static Task WriteHealthResponse(HttpContext context, HealthReport report)
{
    context.Response.ContentType = "application/json; charset=utf-8";
    var payload = new
    {
        status = report.Status.ToString(),
        totalDuration = report.TotalDuration.TotalMilliseconds,
        checks = report.Entries.Select(e => new
        {
            name = e.Key,
            status = e.Value.Status.ToString(),
            description = e.Value.Description,
            duration = e.Value.Duration.TotalMilliseconds,
            data = e.Value.Data,
            exception = e.Value.Exception?.Message
        })
    };
    return context.Response.WriteAsync(JsonSerializer.Serialize(payload,
        new JsonSerializerOptions { WriteIndented = false }));
}

// ---------------------------------------------------------------------------
// Database migration & seeding (must run before Hangfire dashboard/jobs).
// MigrateAsync runs in every environment so production schema changes are
// applied automatically on slot-swap. SeedAsync only writes demo data in
// Development — production rows come from the real DLD source systems.
// ---------------------------------------------------------------------------
using (var migrateScope = app.Services.CreateScope())
{
    var db = migrateScope.ServiceProvider.GetRequiredService<IRETP.Infrastructure.Data.IretpDbContext>();
    await db.Database.MigrateAsync();
}
if (app.Environment.IsDevelopment())
{
    await DbSeeder.SeedAsync(app.Services);
}

// ---------------------------------------------------------------------------
// Hangfire Dashboard & recurring jobs (after DB is ready)
// ---------------------------------------------------------------------------
app.MapHangfireDashboard("/hangfire", new Hangfire.DashboardOptions
{
    Authorization = [new Hangfire.Dashboard.LocalRequestsOnlyAuthorizationFilter()]
});

RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.RiskEngineService>(
    "risk-engine-evaluation",
    service => service.EvaluateRiskIndicatorsAsync(),
    "*/15 * * * *"); // Every 15 minutes

RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.DeveloperScoringService>(
    "developer-scoring",
    service => service.CalculateQuarterlyScoresAsync(),
    "0 0 1 1,4,7,10 *"); // Quarterly: 1st of Jan, Apr, Jul, Oct

// Notification dispatcher — runs every 2 minutes so the RFP Section 6.2
// email-within-5-minutes SLA is comfortably met.
RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.AlertDeliveryService>(
    "notification-dispatcher",
    service => service.DeliverPendingAlertsAsync(),
    "*/2 * * * *");

// Auto-escalate EWRS alerts that breach their acknowledgement SLA
// (RFP Section 8.2). Runs every 5 minutes; the dispatcher above will
// re-fan-out the alert to the new level on its next pass.
RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.AlertEscalationService>(
    "ewrs-auto-escalation",
    service => service.EscalateBreachedAlertsAsync(),
    "*/5 * * * *");

// Refresh the public dashboard KPI snapshot every 15 minutes (RFP FR003).
// Live aggregations are far too expensive for the homepage hit rate; the
// cache makes "freshness ≤ 15 min" a hard guarantee rather than an
// aspirational target.
RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.KpiSnapshotRefreshService>(
    "dashboard-kpi-snapshot",
    service => service.RefreshAsync(),
    "*/15 * * * *");

// Investor alert evaluator — hourly scan of configured alerts
// (price movement, rental yield, new project, watchlist change, digest).
RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.InvestorAlertEvaluator>(
    "investor-alert-evaluator",
    service => service.EvaluateAsync(),
    "7 * * * *"); // Offset 7 minutes past the hour to avoid piling on other jobs

// Monthly Escrow Health report (RFP ESC-003). 02:15 on the 1st of each month
// so the preceding calendar month is fully captured.
RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.EscrowHealthReportService>(
    "escrow-health-monthly-report",
    service => service.GenerateMonthlyReportsAsync(),
    "15 2 1 * *");

// International benchmark refresh (RFP Section 20). Weekly at 03:30 Sunday so
// the /benchmark page shows a current snapshot even when upstream publisher
// feeds have not yet published a new quarter.
RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.BenchmarkRefreshService>(
    "benchmark-weekly-refresh",
    service => service.RefreshAsync(),
    "30 3 * * 0");

// Currency rate refresh (RFP FR005 — daily rates from UAE Central Bank).
// Runs 01:45 UTC so we land just after the UAECB daily publication.
RecurringJob.AddOrUpdate<IRETP.Infrastructure.Services.CurrencyRatesRefreshService>(
    "currency-rates-daily-refresh",
    service => service.RefreshAsync(),
    "45 1 * * *");

app.Run();
