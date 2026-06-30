using IRETP.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace IRETP.WebAPI.Middleware;

/// <summary>
/// Attribute-level filter that enforces the RFP 10.3 rule: Public Visitors
/// must solve a CAPTCHA before exporting data. Authenticated users skip the
/// check. Missing or invalid tokens produce HTTP 403 — we use 403 rather than
/// 401 because the user is authorized to see the endpoint, just not yet
/// verified.
/// </summary>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public class CaptchaRequiredAttribute : Attribute, IAsyncAuthorizationFilter
{
    public const string HeaderName = "X-Captcha-Token";

    public Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (context.HttpContext.User?.Identity?.IsAuthenticated == true)
        {
            return Task.CompletedTask;
        }

        var token = context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var values)
            ? values.ToString()
            : null;

        var service = context.HttpContext.RequestServices.GetService(typeof(ICaptchaService)) as ICaptchaService;
        if (service is null || string.IsNullOrWhiteSpace(token) || !service.ValidateToken(token))
        {
            context.Result = new ObjectResult(new
            {
                error = "CAPTCHA required for anonymous export.",
                hint = "Call GET /api/captcha/challenge and POST /api/captcha/verify to obtain a token, " +
                       "then send it as the X-Captcha-Token header."
            })
            {
                StatusCode = StatusCodes.Status403Forbidden
            };
        }

        return Task.CompletedTask;
    }
}
