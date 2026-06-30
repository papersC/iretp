using IRETP.Domain.Entities;
using IRETP.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace IRETP.WebAPI.Middleware;

/// <summary>
/// Validates API keys for Open Data endpoints (<c>/api/v1/open-data/*</c>).
/// Expects header: X-Api-Key. Path intentionally matches the controller
/// route — earlier builds had a stale <c>/api/opendata</c> check that
/// silently bypassed authentication.
/// </summary>
public class ApiKeyMiddleware
{
    private const string OpenDataPathPrefix = "/api/v1/open-data";

    private readonly RequestDelegate _next;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ApiKeyMiddleware> _logger;

    public ApiKeyMiddleware(RequestDelegate next, IServiceScopeFactory scopeFactory, ILogger<ApiKeyMiddleware> logger)
    {
        _next = next;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Only apply to Open Data endpoints.
        if (!context.Request.Path.StartsWithSegments(OpenDataPathPrefix, StringComparison.OrdinalIgnoreCase))
        {
            await _next(context);
            return;
        }

        if (!context.Request.Headers.TryGetValue("X-Api-Key", out var extractedApiKey))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "API key is required. Include X-Api-Key header." });
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var apiKeyRepo = scope.ServiceProvider.GetRequiredService<IRepository<ApiKey>>();

        var apiKey = await apiKeyRepo.Query()
            .FirstOrDefaultAsync(k => k.Key == extractedApiKey.ToString() && k.IsActive);

        if (apiKey == null)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "Invalid or inactive API key." });
            return;
        }

        if (apiKey.ExpiresAt.HasValue && apiKey.ExpiresAt.Value < DateTime.UtcNow)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { message = "API key has expired." });
            return;
        }

        // Increment usage counter
        apiKey.RequestCount++;
        apiKeyRepo.Update(apiKey);
        var unitOfWork = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
        await unitOfWork.SaveChangesAsync();

        // Store API key info in context for rate limiting etc.
        context.Items["ApiKeyId"] = apiKey.Id;
        context.Items["ApiKeyUserId"] = apiKey.UserId;

        await _next(context);
    }
}

public static class ApiKeyMiddlewareExtensions
{
    public static IApplicationBuilder UseApiKeyValidation(this IApplicationBuilder builder)
    {
        return builder.UseMiddleware<ApiKeyMiddleware>();
    }
}
