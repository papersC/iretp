using IRETP.Infrastructure.Services.Notifications;
using Microsoft.Extensions.Configuration;

namespace IRETP.Tests.Infrastructure;

/// <summary>
/// RFP §6.2 + RFC 8058 one-click unsubscribe tokens. Verifies that tokens
/// are keyed to (userId, reason), that tampering fails, and that rotating
/// the secret invalidates outstanding tokens.
/// </summary>
public class HmacUnsubscribeTokenServiceTests
{
    private static IConfiguration ConfigWith(string secret) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Notifications:UnsubscribeSecret"] = secret
            })
            .Build();

    [Fact]
    public void Mint_then_Verify_round_trips()
    {
        var svc = new HmacUnsubscribeTokenService(ConfigWith("secret-A"));

        var token = svc.Mint("user-123", "marketing");

        Assert.False(string.IsNullOrWhiteSpace(token));
        Assert.True(svc.Verify("user-123", "marketing", token));
    }

    [Fact]
    public void Verify_rejects_wrong_user()
    {
        var svc = new HmacUnsubscribeTokenService(ConfigWith("secret-A"));
        var token = svc.Mint("user-123", "marketing");

        Assert.False(svc.Verify("user-456", "marketing", token));
    }

    [Fact]
    public void Verify_rejects_wrong_reason()
    {
        var svc = new HmacUnsubscribeTokenService(ConfigWith("secret-A"));
        var token = svc.Mint("user-123", "marketing");

        Assert.False(svc.Verify("user-123", "ai-memory", token));
    }

    [Fact]
    public void Secret_rotation_invalidates_outstanding_tokens()
    {
        var oldSvc = new HmacUnsubscribeTokenService(ConfigWith("secret-A"));
        var newSvc = new HmacUnsubscribeTokenService(ConfigWith("secret-B"));

        var token = oldSvc.Mint("user-123", "marketing");
        Assert.True(oldSvc.Verify("user-123", "marketing", token));
        Assert.False(newSvc.Verify("user-123", "marketing", token));
    }

    [Fact]
    public void Empty_token_is_rejected()
    {
        var svc = new HmacUnsubscribeTokenService(ConfigWith("secret-A"));
        Assert.False(svc.Verify("user-123", "marketing", ""));
        Assert.False(svc.Verify("user-123", "marketing", "   "));
    }

    [Fact]
    public void Tokens_are_url_safe()
    {
        var svc = new HmacUnsubscribeTokenService(ConfigWith("secret-A"));
        var token = svc.Mint("user-123", "marketing");
        Assert.DoesNotContain('+', token);
        Assert.DoesNotContain('/', token);
        Assert.DoesNotContain('=', token);
    }
}
