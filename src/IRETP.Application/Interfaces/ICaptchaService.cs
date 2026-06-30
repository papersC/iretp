namespace IRETP.Application.Interfaces;

/// <summary>
/// Lightweight CAPTCHA for public export endpoints (RFP Section 10.3 RBAC —
/// "Export requires CAPTCHA verification" for Public Visitors). Anonymous
/// users request a challenge, submit an answer, receive a signed token, and
/// attach that token as <c>X-Captcha-Token</c> on the subsequent export call.
/// </summary>
public interface ICaptchaService
{
    CaptchaChallenge CreateChallenge();

    /// <summary>
    /// Validates an answer against a challenge id and returns a signed token
    /// on success. Returns null if the challenge has expired, was not issued,
    /// or the answer is wrong.
    /// </summary>
    string? VerifyAnswer(string challengeId, string answer);

    /// <summary>
    /// Returns true when the token is valid and unexpired. Consumed tokens
    /// remain valid for subsequent exports within the same session so users
    /// don't solve a fresh CAPTCHA on every download.
    /// </summary>
    bool ValidateToken(string token);
}

public sealed record CaptchaChallenge(string ChallengeId, string Prompt, DateTime ExpiresAt);
