using Microsoft.JSInterop;

namespace IRETP.Web.Services;

/// <summary>
/// Thin helper that lets any export page enforce the public-visitor CAPTCHA
/// flow (RFP 10.3) with a single call: <see cref="EnsureVerifiedAsync"/>.
/// Authenticated users pass through transparently. Anonymous users are
/// prompted with a JS <c>window.prompt</c> for the challenge answer; a
/// successful response caches a token inside <see cref="WebApiClient"/> for
/// subsequent exports in the same circuit.
/// </summary>
public class CaptchaGateService
{
    private readonly WebApiClient _api;
    private readonly AuthStateService _auth;
    private readonly IJSRuntime _js;

    public CaptchaGateService(WebApiClient api, AuthStateService auth, IJSRuntime js)
    {
        _api = api;
        _auth = auth;
        _js = js;
    }

    public async Task<bool> EnsureVerifiedAsync()
    {
        // Signed-in investors are exempt per the RBAC matrix.
        if (_auth.IsAuthenticated) return true;

        // Cached token? Good to go.
        if (!string.IsNullOrEmpty(_api.CaptchaToken)) return true;

        var challenge = await _api.GetCaptchaChallengeAsync();
        if (challenge is null)
        {
            await _js.InvokeVoidAsync("alert",
                "Unable to reach the CAPTCHA service. Exports are temporarily unavailable.");
            return false;
        }

        string? answer;
        try
        {
            answer = await _js.InvokeAsync<string?>(
                "prompt",
                $"Please solve this quick CAPTCHA before exporting:\n\n{challenge.Prompt}");
        }
        catch
        {
            answer = null;
        }

        if (string.IsNullOrWhiteSpace(answer)) return false;

        var token = await _api.VerifyCaptchaAsync(challenge.ChallengeId, answer);
        if (string.IsNullOrEmpty(token))
        {
            await _js.InvokeVoidAsync("alert", "Incorrect answer — export cancelled. Please try again.");
            return false;
        }

        return true;
    }
}
