using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using PreschoolConnect.Application.Identity;
using Xunit;

namespace PreschoolConnect.Tests;

public class HealthEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly CustomWebApplicationFactory _factory;

    public HealthEndpointTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });
    }

    [Fact]
    public async Task HealthEndpoint_ReturnsOk()
    {
        var response = await _client.GetAsync("/api/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();
        Assert.NotNull(payload);
        Assert.Equal("ok", payload!.status);
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsOk_WhenDatabaseCanConnect()
    {
        var response = await _client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ReadinessResponse>();
        Assert.NotNull(payload);
        Assert.Equal("ready", payload!.status);
        Assert.Equal("connected", payload.database);
    }

    [Fact]
    public async Task ReadinessEndpoint_ReturnsServiceUnavailable_WhenDatabaseCannotConnect()
    {
        using var factory = new CustomWebApplicationFactory { UseInMemoryDatabase = false };
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ReadinessResponse>();
        Assert.NotNull(payload);
        Assert.Equal("not_ready", payload!.status);
        Assert.Equal("unavailable", payload.database);
    }

    [Fact]
    public async Task Login_WithInactiveUser_ReturnsUnauthorized()
    {
        var response = await PostWithCsrfAsync(_client, "/api/auth/login", new
        {
            email = "inactive@northside.test",
            password = "InactivePass!23"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithKnownUser_ReturnsOk()
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync("admin@northside.test");

        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = "admin@northside.test",
                Email = "admin@northside.test",
                IsActive = true,
                EmailConfirmed = true
            };

            await userManager.CreateAsync(user, "AdminPass!23");
        }

        var response = await PostWithCsrfAsync(_client, "/api/auth/login", new
        {
            email = "admin@northside.test",
            password = "AdminPass!23"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(payload);
        Assert.Equal("Login successful.", payload!.message);
    }

    [Fact]
    public async Task Login_WithoutAntiforgeryToken_ReturnsBadRequest()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/login", new
        {
            email = "inactive@northside.test",
            password = "InactivePass!23"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task AntiforgeryEndpoint_WorksOverHttpInDevelopment()
    {
        using var client = _factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            HandleCookies = true
        });

        var response = await client.GetAsync("/api/auth/csrf");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<CsrfResponse>();
        Assert.NotNull(payload);
        Assert.False(string.IsNullOrWhiteSpace(payload!.requestToken));
    }

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object payload)
    {
        var tokenResponse = await client.GetAsync("/api/auth/csrf");
        tokenResponse.EnsureSuccessStatusCode();
        var tokenPayload = await tokenResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        Assert.NotNull(tokenPayload);

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", tokenPayload!.requestToken);
        return await client.SendAsync(request);
    }

    private sealed class HealthResponse
    {
        public string status { get; set; } = string.Empty;
    }

    private sealed class LoginResponse
    {
        public string message { get; set; } = string.Empty;
    }

    private sealed class ReadinessResponse
    {
        public string status { get; set; } = string.Empty;
        public string database { get; set; } = string.Empty;
    }

    private sealed class CsrfResponse
    {
        public string requestToken { get; set; } = string.Empty;
    }
}
