namespace Daraban.Modules.Identity.Services.Auth;

public sealed record RegisterRequest(string Username, string Email, string Password, string DisplayName);
public sealed record LoginRequest(string UsernameOrEmail, string Password);

/// <summary>The refresh token itself is never in this DTO -- it goes in an HttpOnly cookie
/// (Task 1.3 SS3), set directly by the controller. This is deliberately the only place that
/// distinction is enforced -- AuthService returns the raw refresh token string precisely so
/// the controller (which owns HTTP concerns) decides how it's transported, but callers other
/// than the controller should never persist or log RefreshToken.</summary>
public sealed record AuthResult(string AccessToken, DateTimeOffset AccessTokenExpiresAt, string RefreshToken, AuthUserResponse User);

public sealed record AuthUserResponse(Guid Id, string Username, string Email, string DisplayName, Guid ActiveEntityId);
