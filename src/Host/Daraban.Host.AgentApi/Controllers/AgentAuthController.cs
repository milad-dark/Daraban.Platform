using Daraban.Modules.Identity.Services.Agents;
using Daraban.Platform.Common;
using Microsoft.AspNetCore.Mvc;

namespace Daraban.Host.AgentApi.Controllers;

/// <summary>
/// OAuth2 token endpoint for agent client_credentials flow (Task 4.1 SS1.1).
/// Agents POST their client_id + client_secret here to receive a short-lived JWT.
/// No user session, no refresh tokens — agents re-authenticate when the token expires.
/// </summary>
[ApiController]
[Route("api/v1/agents/auth")]
public class AgentAuthController : ControllerBase
{
    private readonly IAgentAuthService _authService;

    public AgentAuthController(IAgentAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// Exchange client credentials for an access token (OAuth2 client_credentials grant).
    /// POST /api/v1/agents/auth/token
    /// Body: { "clientId": "da_...", "clientSecret": "...", "scope": "inventory:write" }
    /// </summary>
    [HttpPost("token")]
    public async Task<IActionResult> GetToken([FromBody] TokenRequest request, CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var userAgent = Request.Headers.UserAgent.ToString();

        var result = await _authService.GetTokenAsync(request, ipAddress, userAgent, ct);
        if (!result.IsSuccess) return ProblemFrom(result.Error!);

        return Ok(result.Value);
    }

    private ObjectResult ProblemFrom(Error error)
    {
        var status = error.Type switch
        {
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Forbidden => StatusCodes.Status403Forbidden,
            _ => StatusCodes.Status400BadRequest,
        };
        return new ObjectResult(new ProblemDetails
        {
            Title = error.Message,
            Status = status,
            Extensions = { ["errorCode"] = error.Code },
        }) { StatusCode = status };
    }
}
