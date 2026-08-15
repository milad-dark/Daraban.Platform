using System.Security.Claims;
using Daraban.Platform.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Daraban.Platform.Hosting.Authorization;

/// <summary>Resolved once per request from the validated JWT's claims (Task 1.3 SS2.1) --
/// sub (user id) and active_entity_id, exactly as JwtTokenService issues them.</summary>
public class CurrentUser : ICurrentUser
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    public CurrentUser(IHttpContextAccessor httpContextAccessor) => _httpContextAccessor = httpContextAccessor;

    private ClaimsPrincipal? Principal => _httpContextAccessor.HttpContext?.User;

    public bool IsAuthenticated => Principal?.Identity?.IsAuthenticated ?? false;

    public Guid UserId =>
        Guid.TryParse(Principal?.FindFirst(System.IdentityModel.Tokens.Jwt.JwtRegisteredClaimNames.Sub)?.Value, out var id)
            ? id : Guid.Empty;

    public Guid ActiveEntityId =>
        Guid.TryParse(Principal?.FindFirst("active_entity_id")?.Value, out var id)
            ? id : Guid.Empty;
}
