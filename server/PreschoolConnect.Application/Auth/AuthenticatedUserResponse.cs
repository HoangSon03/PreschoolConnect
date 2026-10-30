namespace PreschoolConnect.Application.Auth;

public sealed class AuthenticatedUserResponse
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string[] Roles { get; set; } = Array.Empty<string>();
}
