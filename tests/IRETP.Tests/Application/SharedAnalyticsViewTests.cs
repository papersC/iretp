using IRETP.Application.Features.Analytics.Commands;
using IRETP.Application.Features.Analytics.Queries;
using IRETP.Domain.Entities;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Tests.Application;

/// <summary>
/// RFP AN-006 — shared analysis links. Covers 12-month expiration and
/// round-trip fidelity of the full analysis configuration JSON.
/// </summary>
public class SharedAnalyticsViewTests
{
    private record Env(
        IretpDbContext Db,
        SaveAnalyticsViewCommandHandler Save,
        GetSharedViewQueryHandler Get);

    private static Env Build()
    {
        var options = new DbContextOptionsBuilder<IretpDbContext>()
            .UseInMemoryDatabase(databaseName: $"shared-views-{Guid.NewGuid():N}")
            .Options;

        var db = new IretpDbContext(options);
        var repo = new Repository<SavedAnalyticsView>(db);
        var uow = new UnitOfWork(db);
        return new Env(db, new SaveAnalyticsViewCommandHandler(repo, uow), new GetSharedViewQueryHandler(repo));
    }

    private const string FullConfigJson = """
        {
          "dimensions": ["Zone", "PropertyType"],
          "metrics": ["TransactionCount", "AvgPricePerSqft"],
          "filters": { "dateFrom": "2024-01-01", "dateTo": "2026-04-18", "zones": ["DMR", "JVC"] },
          "chartType": "stackedBar",
          "timePeriod": "Last36M"
        }
        """;

    [Fact]
    public async Task Public_view_gets_share_token_and_twelve_month_expiry()
    {
        var env = Build();
        var id = await env.Save.Handle(new SaveAnalyticsViewCommand
        {
            UserId = "u1",
            Name = "zone breakdown",
            ConfigurationJson = FullConfigJson,
            IsPublic = true
        }, CancellationToken.None);

        var view = await env.Db.SavedAnalyticsViews.SingleAsync(v => v.Id == id);
        Assert.False(string.IsNullOrWhiteSpace(view.ShareToken));
        Assert.NotNull(view.ShareTokenExpiresAt);
        var lifespan = view.ShareTokenExpiresAt!.Value - view.CreatedAt;
        Assert.True(lifespan >= TimeSpan.FromDays(364) && lifespan <= TimeSpan.FromDays(366),
            $"Share token lifetime should be ~365 days, was {lifespan.TotalDays:N1}.");
    }

    [Fact]
    public async Task Private_view_has_no_share_token_or_expiry()
    {
        var env = Build();
        await env.Save.Handle(new SaveAnalyticsViewCommand
        {
            UserId = "u1", Name = "private", ConfigurationJson = "{}", IsPublic = false
        }, CancellationToken.None);

        var view = await env.Db.SavedAnalyticsViews.SingleAsync();
        Assert.Null(view.ShareToken);
        Assert.Null(view.ShareTokenExpiresAt);
    }

    [Fact]
    public async Task Shared_link_restores_full_configuration_json_verbatim()
    {
        var env = Build();
        await env.Save.Handle(new SaveAnalyticsViewCommand
        {
            UserId = "u1",
            Name = "public view",
            ConfigurationJson = FullConfigJson,
            IsPublic = true
        }, CancellationToken.None);

        var saved = await env.Db.SavedAnalyticsViews.SingleAsync();
        var dto = await env.Get.Handle(new GetSharedViewQuery { ShareToken = saved.ShareToken! }, CancellationToken.None);

        Assert.NotNull(dto);
        Assert.Equal(FullConfigJson, dto!.ConfigurationJson);
        Assert.Equal(saved.Name, dto.Name);
        Assert.True(dto.IsPublic);
        Assert.Equal(saved.ShareToken, dto.ShareToken);
        Assert.Equal(saved.ShareTokenExpiresAt, dto.ShareTokenExpiresAt);
    }

    [Fact]
    public async Task Expired_share_link_returns_null()
    {
        var env = Build();
        await env.Save.Handle(new SaveAnalyticsViewCommand
        {
            UserId = "u1", Name = "stale", ConfigurationJson = FullConfigJson, IsPublic = true
        }, CancellationToken.None);

        // Backdate expiry past now. SaveChanges interceptor preserves this
        // when modifying an entity's non-CreatedAt property.
        var saved = await env.Db.SavedAnalyticsViews.SingleAsync();
        saved.ShareTokenExpiresAt = DateTime.UtcNow.AddDays(-1);
        await env.Db.SaveChangesAsync();

        var dto = await env.Get.Handle(new GetSharedViewQuery { ShareToken = saved.ShareToken! }, CancellationToken.None);
        Assert.Null(dto);
    }

    [Fact]
    public async Task Private_view_is_not_reachable_via_shared_link()
    {
        var env = Build();
        env.Db.SavedAnalyticsViews.Add(new SavedAnalyticsView
        {
            Id = Guid.NewGuid(),
            UserId = "u1", Name = "leaked?", ConfigurationJson = "{}",
            IsPublic = false, ShareToken = "guessable-token"
        });
        await env.Db.SaveChangesAsync();

        var dto = await env.Get.Handle(new GetSharedViewQuery { ShareToken = "guessable-token" }, CancellationToken.None);
        Assert.Null(dto);
    }

    [Fact]
    public async Task Unknown_share_token_returns_null()
    {
        var env = Build();
        var dto = await env.Get.Handle(new GetSharedViewQuery { ShareToken = "does-not-exist" }, CancellationToken.None);
        Assert.Null(dto);
    }
}
