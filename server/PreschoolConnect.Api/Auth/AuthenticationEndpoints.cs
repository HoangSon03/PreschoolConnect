using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using PreschoolConnect.Application.Auth;

namespace PreschoolConnect.Api.Auth;

public static class AuthenticationEndpoints
{
    public static void MapAuthenticationEndpoints(this WebApplication app)
    {
        app.MapGet("/api/auth/me", async (HttpContext http, IAuthenticationService auth) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            var user = await auth.GetUserAsync(Guid.Parse(http.User.FindFirstValue("sub")!), http.RequestAborted);
            return user is null ? Results.Unauthorized() : Results.Ok(user);
        }).RequireAuthorization();

        app.MapPost("/api/auth/login", async (LoginRequest request, HttpContext http,
            IAntiforgery antiforgery, IAuthenticationService auth, JwtTokenIssuer issuer) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!await antiforgery.IsRequestValidAsync(http))
                return Results.BadRequest(new { message = "A valid antiforgery token is required." });
            if (string.IsNullOrWhiteSpace(request.Email) || request.Email.Length > 256 ||
                !new EmailAddressAttribute().IsValid(request.Email.Trim()) ||
                string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 1024)
                return Results.BadRequest(new { message = "A valid email and password are required." });
            var login = await auth.LoginAsync(request.Email, request.Password, http.RequestAborted);
            return login is null ? Results.Unauthorized() : Results.Ok(issuer.Issue(login.User, login.SecurityStamp));
        }).RequireRateLimiting("login");

        app.MapPost("/api/auth/logout", async (HttpContext http, IAntiforgery antiforgery, IAuthenticationService auth) =>
        {
            http.Response.Headers.CacheControl = "no-store";
            if (!await antiforgery.IsRequestValidAsync(http))
                return Results.BadRequest(new { message = "A valid antiforgery token is required." });
            return await auth.LogoutAsync(Guid.Parse(http.User.FindFirstValue("sub")!))
                ? Results.Ok(new { message = "All account sessions signed out." }) : Results.Unauthorized();
        }).RequireAuthorization();
    }
}
