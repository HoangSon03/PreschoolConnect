using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Options;
using PreschoolConnect.Application.Auth;

namespace PreschoolConnect.Api.Auth;

public sealed class JwtSettings
{
    public string Issuer { get; set; } = "";
    public string Audience { get; set; } = "";
    public string SigningKey { get; set; } = "";
}

public sealed record AccessTokenResponse(string AccessToken, string TokenType, int ExpiresIn,
    DateTimeOffset ExpiresAtUtc, AuthenticatedUser User);

public sealed class JwtTokenIssuer(JwtSettings settings)
{
    public const int LifetimeSeconds = 900;

    public string SessionMarker(string stamp) =>
        Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(settings.SigningKey), Encoding.UTF8.GetBytes(stamp)));

    public AccessTokenResponse Issue(AuthenticatedUser user, string stamp)
    {
        var now = DateTimeOffset.UtcNow;
        var expires = now.AddSeconds(LifetimeSeconds);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new("session", SessionMarker(stamp))
        };
        claims.AddRange(user.Roles.Select(role => new Claim("role", role)));
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience, claims,
            now.UtcDateTime, expires.UtcDateTime,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), "Bearer", LifetimeSeconds, expires, user);
    }
}

public static class JwtAuthentication
{
    public static void AddJwtAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<JwtSettings>().Bind(configuration.GetSection("Jwt"))
            .Validate(settings => !string.IsNullOrWhiteSpace(settings.Issuer) && !string.IsNullOrWhiteSpace(settings.Audience) &&
                !string.IsNullOrWhiteSpace(settings.SigningKey) &&
                Encoding.UTF8.GetByteCount(settings.SigningKey) >= 32,
                "Configure Jwt:Issuer, Jwt:Audience and a random Jwt:SigningKey of at least 32 bytes.")
            .ValidateOnStart();
        services.AddSingleton(provider => provider.GetRequiredService<IOptions<JwtSettings>>().Value);
        services.AddSingleton<JwtTokenIssuer>();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultForbidScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer();
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme).Configure<JwtSettings>((options, settings) =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new()
            {
                ValidateIssuer = true, ValidIssuer = settings.Issuer,
                ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.SigningKey)),
                ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                ClockSkew = TimeSpan.Zero, NameClaimType = "sub", RoleClaimType = "role"
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    if (!Guid.TryParse(context.Principal?.FindFirstValue("sub"), out var id))
                    {
                        context.Fail("Invalid authentication session.");
                        return;
                    }
                    var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthenticationService>();
                    var issuer = context.HttpContext.RequestServices.GetRequiredService<JwtTokenIssuer>();
                    var stamp = await auth.GetSecurityStampAsync(id);
                    if (stamp is null || context.Principal!.FindFirstValue("session") != issuer.SessionMarker(stamp))
                    {
                        context.Fail("Invalid authentication session.");
                        return;
                    }
                    var user = await auth.GetUserAsync(id, context.HttpContext.RequestAborted);
                    if (user is null)
                    {
                        context.Fail("Invalid authentication session.");
                        return;
                    }
                    var identity = (ClaimsIdentity)context.Principal!.Identity!;
                    foreach (var claim in identity.FindAll("role").ToArray()) identity.RemoveClaim(claim);
                    foreach (var role in user.Roles) identity.AddClaim(new("role", role));
                    identity.AddClaim(new(ClaimTypes.NameIdentifier, id.ToString()));
                }
            };
        });
    }
}
