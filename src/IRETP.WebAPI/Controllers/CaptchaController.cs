using IRETP.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace IRETP.WebAPI.Controllers;

[ApiController]
[Route("api/[controller]")]
public class CaptchaController : ControllerBase
{
    private readonly ICaptchaService _captcha;

    public CaptchaController(ICaptchaService captcha)
    {
        _captcha = captcha;
    }

    /// <summary>
    /// Issue a fresh CAPTCHA challenge. The client submits the answer to
    /// <c>POST /api/captcha/verify</c> and attaches the returned token as
    /// <c>X-Captcha-Token</c> on subsequent public export calls (RFP 10.3).
    /// </summary>
    [HttpGet("challenge")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public IActionResult GetChallenge()
    {
        var challenge = _captcha.CreateChallenge();
        return Ok(challenge);
    }

    /// <summary>
    /// Verify a challenge answer and receive a signed export token.
    /// </summary>
    [HttpPost("verify")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public IActionResult Verify([FromBody] CaptchaVerifyRequest request)
    {
        var token = _captcha.VerifyAnswer(request.ChallengeId, request.Answer);
        if (token is null) return BadRequest(new { error = "Incorrect or expired challenge." });
        return Ok(new { token });
    }
}

public class CaptchaVerifyRequest
{
    public string ChallengeId { get; set; } = default!;
    public string Answer { get; set; } = default!;
}
