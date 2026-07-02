using IRETP.Application.Interfaces;
using IRETP.Domain.Interfaces;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Identity;
using IRETP.Infrastructure.Repositories;
using IRETP.Infrastructure.Services;
using IRETP.Infrastructure.Services.Fabric;
using IRETP.Infrastructure.Services.Notifications;
using IRETP.Infrastructure.Services.Rag;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace IRETP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

        // EF Core + SQL Server
        services.AddDbContext<IretpDbContext>(options =>
            options.UseSqlServer(connectionString, sqlOptions =>
            {
                sqlOptions.MigrationsAssembly(typeof(IretpDbContext).Assembly.FullName);
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null);
            }));

        // ASP.NET Identity
        services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                options.Password.RequireDigit = true;
                options.Password.RequireLowercase = true;
                options.Password.RequireUppercase = true;
                options.Password.RequireNonAlphanumeric = true;
                options.Password.RequiredLength = 8;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = false;
            })
            .AddEntityFrameworkStores<IretpDbContext>()
            .AddDefaultTokenProviders();

        // Repositories
        services.AddScoped(typeof(IRepository<>), typeof(Repository<>));
        services.AddScoped<IUnitOfWork, UnitOfWork>();

        // AI Orchestrator
        services.AddHttpClient("AIService");
        services.AddSingleton<IAIModelMetrics, AIModelMetrics>();
        services.AddSingleton<IAdvisoryGuardrail, KeywordAdvisoryGuardrail>();
        services.AddScoped<IAIOrchestrator, AIOrchestrator>();

        // RAG vector store — TF-IDF semantic retrieval over the DLD data that
        // grounds the AI Agent (IVectorStore seam lets a neural embedder swap in
        // if an Azure OpenAI embeddings deployment becomes available).
        services.AddScoped<IVectorStore, TfidfVectorStore>();
        services.AddScoped<IUserConsent, Identity.UserConsentService>();
        services.AddScoped<IAiAccuracyHarness, AiAccuracyHarness>();

        // Meta-Q&A over the Compliance Matrix ("how/why does the build address
        // requirement X") — grounds on docs/COMPLIANCE_MATRIX.md, not DLD data.
        services.AddScoped<IComplianceAsk, ComplianceAskService>();

        // Deterministic time-series analytics for the AI Agent's "deep
        // analysis" capability (RFP AI004). Singleton — pure functions,
        // no per-request state.
        services.AddSingleton<ITimeSeriesAnalyzer, TimeSeriesAnalyzer>();

        // Audit Logging
        services.AddScoped<IAuditLogService, AuditLogService>();

        // Notification senders (RFP Section 6.2)
        services.Configure<NotificationOptions>(configuration.GetSection(NotificationOptions.SectionName));
        services.AddHttpClient("SmsGateway");
        services.AddSingleton<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<ISmsSender, HttpSmsSender>();
        services.AddSingleton<IUnsubscribeTokenService, HmacUnsubscribeTokenService>();
        services.AddScoped<INotificationRecipientResolver, NotificationRecipientResolver>();

        // Real-time broadcaster — NoOp default keeps the Infrastructure free
        // of a SignalR dependency. WebAPI replaces this with a SignalR-backed
        // implementation at startup to light up the real-time path.
        services.AddSingleton<INotificationBroadcaster, NoOpNotificationBroadcaster>();

        // KPI snapshot cache + 15-minute refresher (RFP FR003)
        services.AddSingleton<IKpiSnapshotCache, KpiSnapshotCache>();
        services.AddSingleton<KpiSnapshotRefreshService>();

        // Background Services
        services.AddSingleton<RiskEngineService>();
        services.AddSingleton<DeveloperScoringService>();
        services.AddSingleton<AlertDeliveryService>();
        services.AddSingleton<AlertEscalationService>();
        services.AddSingleton<InvestorAlertEvaluator>();
        services.AddSingleton<EscrowHealthReportService>();
        services.AddSingleton<BenchmarkRefreshService>();
        services.AddSingleton<InvestorScorecardPdfService>();
        services.AddHttpClient("CurrencyRates");
        services.AddSingleton<CurrencyRatesRefreshService>();

        // Public CAPTCHA (RFP 10.3) — singleton so challenges and tokens
        // persist across requests for the life of the process.
        services.AddSingleton<ICaptchaService, SimpleCaptchaService>();

        // Microsoft Fabric / OneLake adapter (RFP v1.3 §11.4). The default
        // passthrough surfaces the contract over the local OLTP store; a
        // production deployment swaps to OneLakeDirect or FabricSemanticModel
        // by flipping the "Fabric:Mode" config value.
        services.Configure<OneLakeFabricOptions>(configuration.GetSection(OneLakeFabricOptions.SectionName));
        services.AddScoped<IFabricGoldDataSource, PassthroughFabricGoldDataSource>();

        return services;
    }
}
