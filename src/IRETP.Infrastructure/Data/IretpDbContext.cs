using IRETP.Domain.Common;
using IRETP.Domain.Entities;
using IRETP.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Infrastructure.Data;

public class IretpDbContext : IdentityDbContext<ApplicationUser>
{
    public IretpDbContext(DbContextOptions<IretpDbContext> options) : base(options) { }

    public DbSet<Zone> Zones => Set<Zone>();
    public DbSet<Developer> Developers => Set<Developer>();
    public DbSet<Project> Projects => Set<Project>();
    public DbSet<ProjectUnit> ProjectUnits => Set<ProjectUnit>();
    public DbSet<Transaction> Transactions => Set<Transaction>();
    public DbSet<PriceIndex> PriceIndices => Set<PriceIndex>();
    public DbSet<RentalIndex> RentalIndices => Set<RentalIndex>();
    public DbSet<EscrowAccount> EscrowAccounts => Set<EscrowAccount>();
    public DbSet<EscrowTransaction> EscrowTransactions => Set<EscrowTransaction>();
    public DbSet<DeveloperScore> DeveloperScores => Set<DeveloperScore>();
    public DbSet<ScoringWeight> ScoringWeights => Set<ScoringWeight>();
    public DbSet<RegulatoryViolation> RegulatoryViolations => Set<RegulatoryViolation>();
    public DbSet<RiskAlert> RiskAlerts => Set<RiskAlert>();
    public DbSet<RiskThreshold> RiskThresholds => Set<RiskThreshold>();
    public DbSet<InvestorAlert> InvestorAlerts => Set<InvestorAlert>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WatchlistItem> WatchlistItems => Set<WatchlistItem>();
    public DbSet<SavedAnalyticsView> SavedAnalyticsViews => Set<SavedAnalyticsView>();
    public DbSet<CmsContent> CmsContents => Set<CmsContent>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<ProjectCertification> ProjectCertifications => Set<ProjectCertification>();
    public DbSet<MarketBenchmark> MarketBenchmarks => Set<MarketBenchmark>();
    public DbSet<BeneficialOwner> BeneficialOwners => Set<BeneficialOwner>();
    public DbSet<CmsContentVersion> CmsContentVersions => Set<CmsContentVersion>();
    public DbSet<AiInteractionLog> AiInteractionLogs => Set<AiInteractionLog>();
    public DbSet<NameValidation> NameValidations => Set<NameValidation>();
    public DbSet<CurrencyRate> CurrencyRates => Set<CurrencyRate>();
    public DbSet<UserAiMemory> UserAiMemories => Set<UserAiMemory>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        foreach (var entry in ChangeTracker.Entries<BaseEntity>())
        {
            // RFP §10.2 Audit trail immutability — AuditLog rows are
            // append-only. Any attempt to modify or delete an existing row
            // is refused at this boundary so no upstream bug (or compromised
            // admin handler) can rewrite history.
            if (entry.Entity is AuditLog && entry.State is EntityState.Modified or EntityState.Deleted)
            {
                throw new InvalidOperationException(
                    "AuditLog rows are immutable (RFP §10.2). " +
                    $"Rejected {entry.State} on AuditLog id={entry.Entity.Id}.");
            }

            switch (entry.State)
            {
                case EntityState.Added:
                    if (entry.Entity.Id == Guid.Empty)
                        entry.Entity.Id = Guid.NewGuid();
                    entry.Entity.CreatedAt = now;
                    break;

                case EntityState.Modified:
                    entry.Entity.UpdatedAt = now;
                    entry.Property(nameof(BaseEntity.CreatedAt)).IsModified = false;
                    entry.Property(nameof(BaseEntity.CreatedBy)).IsModified = false;
                    break;
            }
        }

        return await base.SaveChangesAsync(cancellationToken);
    }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.ApplyConfigurationsFromAssembly(typeof(IretpDbContext).Assembly);
    }
}
