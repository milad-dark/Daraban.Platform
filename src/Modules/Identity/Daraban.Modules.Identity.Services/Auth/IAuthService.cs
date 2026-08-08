using Daraban.Platform.Common;

namespace Daraban.Modules.Identity.Services.Auth;

public interface IAuthService
{
    Task<Result<AuthUserResponse>> RegisterAsync(RegisterRequest request, CancellationToken ct = default);
    Task<Result<AuthResult>> LoginAsync(LoginRequest request, string? ip, string? userAgent, CancellationToken ct = default);
    Task<Result<AuthResult>> RefreshAsync(string presentedRefreshToken, CancellationToken ct = default);
    Task LogoutAsync(string presentedRefreshToken, CancellationToken ct = default);
}
