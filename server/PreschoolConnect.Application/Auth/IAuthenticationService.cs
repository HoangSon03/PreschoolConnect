namespace PreschoolConnect.Application.Auth;

public sealed record AuthenticatedUser(Guid UserId, string Email, string FullName, bool IsActive, string[] Roles);
public sealed record AuthenticatedLogin(AuthenticatedUser User, string SecurityStamp);
public sealed class AuthenticationSchemaNotReadyException()
    : Exception("Authentication requires the pending database migrations to be applied by an operator.");

public interface IAuthenticationService
{
    Task<AuthenticatedLogin?> LoginAsync(string email, string password, CancellationToken cancellationToken);
    Task<AuthenticatedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken);
    Task<bool> LogoutAsync(Guid userId);
    Task<string?> GetSecurityStampAsync(Guid userId);
}
