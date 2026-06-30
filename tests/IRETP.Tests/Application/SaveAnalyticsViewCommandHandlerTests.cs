using IRETP.Application.Features.Analytics.Commands;
using IRETP.Domain.Entities;
using IRETP.Infrastructure.Data;
using IRETP.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace IRETP.Tests.Application;

public class SaveAnalyticsViewCommandHandlerTests
{
    private static (IretpDbContext db, SaveAnalyticsViewCommandHandler handler) Build()
    {
        var options = new DbContextOptionsBuilder<IretpDbContext>()
            .UseInMemoryDatabase(databaseName: $"saved-views-{Guid.NewGuid():N}")
            .Options;

        var db = new IretpDbContext(options);
        var repo = new Repository<SavedAnalyticsView>(db);
        var uow = new UnitOfWork(db);
        return (db, new SaveAnalyticsViewCommandHandler(repo, uow));
    }

    private static SaveAnalyticsViewCommand Cmd(string userId, string name = "view") => new()
    {
        UserId = userId,
        Name = name,
        ConfigurationJson = "{}",
        IsPublic = false,
        DisplayOrder = 0
    };

    [Fact]
    public async Task Saves_the_view_when_under_the_cap()
    {
        var (db, handler) = Build();
        var id = await handler.Handle(Cmd("user-1", "first"), CancellationToken.None);

        Assert.NotEqual(Guid.Empty, id);
        Assert.Equal(1, db.SavedAnalyticsViews.Count(v => v.UserId == "user-1"));
    }

    [Fact]
    public async Task Allows_exactly_twelve_views_per_user()
    {
        var (_, handler) = Build();
        for (var i = 0; i < SaveAnalyticsViewCommandHandler.MaxSavedViewsPerUser; i++)
        {
            await handler.Handle(Cmd("user-1", $"view-{i}"), CancellationToken.None);
        }

        // Twelve must succeed; the thirteenth must throw.
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            handler.Handle(Cmd("user-1", "view-13"), CancellationToken.None));

        Assert.Contains("12", ex.Message);
        Assert.Contains("AN003", ex.Message);
    }

    [Fact]
    public async Task Cap_is_per_user_not_global()
    {
        var (db, handler) = Build();
        for (var i = 0; i < SaveAnalyticsViewCommandHandler.MaxSavedViewsPerUser; i++)
        {
            await handler.Handle(Cmd("user-1", $"view-{i}"), CancellationToken.None);
        }

        // A different user must still be able to save their first view.
        var newUserId = await handler.Handle(Cmd("user-2", "first"), CancellationToken.None);
        Assert.NotEqual(Guid.Empty, newUserId);
        Assert.Equal(SaveAnalyticsViewCommandHandler.MaxSavedViewsPerUser,
            db.SavedAnalyticsViews.Count(v => v.UserId == "user-1"));
        Assert.Equal(1, db.SavedAnalyticsViews.Count(v => v.UserId == "user-2"));
    }

    [Fact]
    public async Task Missing_user_id_is_rejected()
    {
        var (_, handler) = Build();
        await Assert.ThrowsAsync<ArgumentException>(() =>
            handler.Handle(new SaveAnalyticsViewCommand
            {
                UserId = null,
                Name = "x",
                ConfigurationJson = "{}"
            }, CancellationToken.None));
    }
}
