using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using PreschoolConnect.Api.Auth;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Identity;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;

namespace PreschoolConnect.Tests;

public sealed class JwtAuthenticationTests
{
    private const string Password = "SyntheticAuth!123";

    [Fact]
    public async Task LoginIssuesMinimalShortLivedBearerWithoutAuthCookie()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, id, _) = await CreateAccount(factory);
        using var client = Client(factory);
        var response = await Post(client, "/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        var result = await response.Content.ReadFromJsonAsync<AccessTokenResponse>();
        Assert.NotNull(result);
        Assert.Equal(900, result.ExpiresIn);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Contains("Admin", result.User.Roles);
        Assert.DoesNotContain("SchoolAdmin", result.User.Roles);
        Assert.InRange(result.ExpiresAtUtc, DateTimeOffset.UtcNow.AddMinutes(14), DateTimeOffset.UtcNow.AddMinutes(16));
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.AccessToken);
        Assert.Equal(id.ToString(), jwt.Subject);
        Assert.DoesNotContain(jwt.Claims, claim => claim.Type.Contains("email") || claim.Type.Contains("password") ||
            claim.Type.Contains("stamp", StringComparison.OrdinalIgnoreCase) || claim.Type.Contains("School"));
        Assert.False(response.Headers.TryGetValues("Set-Cookie", out var cookies) &&
            cookies.Any(cookie => cookie.StartsWith("PreschoolConnect.Auth=")));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        client.DefaultRequestHeaders.Authorization = new("Bearer", result.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth-test/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth-test/teacher")).StatusCode);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("wrong-password")]
    [InlineData("inactive")]
    [InlineData("locked")]
    public async Task InvalidCredentialsHaveTheSameGenericResponse(string scenario)
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, id, _) = await CreateAccount(factory);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(id.ToString()))!;
            if (scenario == "inactive") { user.IsActive = false; Assert.True((await users.UpdateAsync(user)).Succeeded); }
            if (scenario == "locked")
                Assert.True((await users.SetLockoutEndDateAsync(user, DateTimeOffset.UtcNow.AddMinutes(5))).Succeeded);
        }
        using var client = Client(factory);
        var response = await Post(client, "/api/auth/login", new
        {
            email = scenario == "missing" ? "missing@synthetic.test" : email,
            password = scenario == "wrong-password" ? "Incorrect!123" : Password
        });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.DoesNotContain(email, await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("", "Password!123")]
    [InlineData("bad-email", "Password!123")]
    [InlineData("valid@synthetic.test", "")]
    public async Task InvalidLoginReturns400(string email, string password)
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/api/auth/login", new { email, password })).StatusCode);
    }

    [Fact]
    public async Task LoginRejectsOversizedInputAndOwnershipOverposting()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        using var client = Client(factory);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/api/auth/login",
            new { email = new string('x', 257), password = Password })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/api/auth/login",
            new { email = "synthetic@synthetic.test", password = new string('x', 1025) })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(client, "/api/auth/login",
            new { email = "synthetic@synthetic.test", password = Password, schoolId = Guid.NewGuid() })).StatusCode);
    }

    [Theory]
    [InlineData("issuer")]
    [InlineData("audience")]
    [InlineData("key")]
    [InlineData("expired")]
    [InlineData("future")]
    [InlineData("algorithm")]
    [InlineData("unsigned")]
    [InlineData("session")]
    [InlineData("missing-sub")]
    [InlineData("missing-session")]
    [InlineData("no-expiry")]
    public async Task InvalidTokensAreRejected(string scenario)
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, _, _) = await CreateAccount(factory);
        using var client = Client(factory);
        var login = await Login(client, email);
        var original = new JwtSecurityTokenHandler().ReadJwtToken(login.AccessToken);
        var settings = factory.Services.GetRequiredService<JwtSettings>();
        var claims = original.Claims.Where(claim => claim.Type is "sub" or "session" or "role").ToList();
        if (scenario == "session")
        {
            claims.RemoveAll(claim => claim.Type == "session");
            claims.Add(new("session", "invalid-marker"));
        }
        if (scenario == "missing-sub") claims.RemoveAll(claim => claim.Type == "sub");
        if (scenario == "missing-session") claims.RemoveAll(claim => claim.Type == "session");
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            scenario == "issuer" ? "wrong" : settings.Issuer,
            scenario == "audience" ? "wrong" : settings.Audience,
            claims,
            scenario == "expired" ? now.AddMinutes(-20) : scenario == "future" ? now.AddMinutes(1) : now,
            scenario == "no-expiry" ? null : scenario == "expired" ? now.AddMinutes(-1) : now.AddMinutes(15),
            scenario == "unsigned" ? null : new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(scenario == "key"
                    ? new string('x', 64) : settings.SigningKey)),
                scenario == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization = new("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task LogoutRevokesEveryTokenAndRequiresAuthenticatedCsrf()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, _, _) = await CreateAccount(factory);
        using var client = Client(factory);
        using var second = Client(factory);
        var firstLogin = await Login(client, email);
        var secondLogin = await Login(second, email);
        client.DefaultRequestHeaders.Authorization = new("Bearer", firstLogin.AccessToken);
        second.DefaultRequestHeaders.Authorization = new("Bearer", secondLogin.AccessToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsync("/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/logout", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await second.GetAsync("/api/auth/me")).StatusCode);
        using var fresh = Client(factory);
        Assert.NotEmpty((await Login(fresh, email)).AccessToken);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("inactive")]
    [InlineData("membership")]
    public async Task AccountAndMembershipChangesTakeEffectImmediately(string change)
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, id, _) = await CreateAccount(factory);
        using var client = Client(factory);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await Login(client, email)).AccessToken);
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(id.ToString()))!;
            if (change == "password")
                Assert.True((await users.ResetPasswordAsync(user, await users.GeneratePasswordResetTokenAsync(user), "ChangedAuth!456")).Succeeded);
            if (change == "inactive") { user.IsActive = false; Assert.True((await users.UpdateAsync(user)).Succeeded); }
            if (change == "membership")
            {
                using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("test-revoke");
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                (await db.SchoolMemberships.SingleAsync(member => member.UserId == id)).IsActive = false;
                await db.SaveChangesAsync();
            }
        }
        Assert.Equal(change == "membership" ? HttpStatusCode.Forbidden : HttpStatusCode.Unauthorized,
            (await client.GetAsync("/api/auth-test/admin")).StatusCode);
    }

    [Theory]
    [InlineData("Teacher", "teacher")]
    [InlineData("Parent", "parent")]
    public async Task MembershipRolePoliciesAreEnforced(string role, string route)
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, _, _) = await CreateAccount(factory, role);
        using var client = Client(factory);
        var result = await Login(client, email);
        Assert.Equal(new[] { role }, result.User.Roles);
        client.DefaultRequestHeaders.Authorization = new("Bearer", result.AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/auth-test/{route}")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/auth-test/admin")).StatusCode);
    }

    [Fact]
    public async Task ProvisioningCreatesHashedSchoolAdminAndNeverElevatesExistingAccount()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (_, _, school) = await CreateAccount(factory, "Teacher");
        using var scope = factory.Services.CreateScope();
        var provisioner = scope.ServiceProvider.GetRequiredService<AdminProvisioner>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        const string email = "bootstrap@synthetic.test";
        await provisioner.ProvisionAsync(email, Password, school);
        var user = (await users.FindByEmailAsync(email))!;
        Assert.NotEqual(Password, user.PasswordHash);
        Assert.True(await users.CheckPasswordAsync(user, Password));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionAsync(email, "Different!123", school));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionAsync("wrong-school@synthetic.test", Password, Guid.NewGuid()));
        await Assert.ThrowsAsync<InvalidOperationException>(() => provisioner.ProvisionAsync("weak@synthetic.test", "weak", school));
        Assert.Null(await users.FindByEmailAsync("weak@synthetic.test"));
        using var client = Client(factory);
        Assert.Contains("Admin", (await Login(client, email)).User.Roles);
    }

    [Fact]
    public async Task RepeatedWrongPasswordsTriggerIdentityLockout()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, id, _) = await CreateAccount(factory);
        using var client = Client(factory);
        for (var attempt = 0; attempt < 5; attempt++)
            Assert.Equal(HttpStatusCode.Unauthorized,
                (await Post(client, "/api/auth/login", new { email, password = "Wrong!123" })).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await Post(client, "/api/auth/login", new { email, password = Password })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        Assert.True(await users.IsLockedOutAsync((await users.FindByIdAsync(id.ToString()))!));
    }

    [Fact]
    public async Task LoginTokenRemainsValidWhenIdentityUpgradesAnOlderPasswordHash()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, id, _) = await CreateAccount(factory);
        string oldHash;
        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await users.FindByIdAsync(id.ToString()))!;
            var hasher = new PasswordHasher<ApplicationUser>(
                Microsoft.Extensions.Options.Options.Create(new PasswordHasherOptions { IterationCount = 1000 }));
            oldHash = hasher.HashPassword(user, Password);
            user.PasswordHash = oldHash;
            Assert.True((await users.UpdateAsync(user)).Succeeded);
        }
        using var client = Client(factory);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await Login(client, email)).AccessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
        using var verification = factory.Services.CreateScope();
        var stored = await verification.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(id.ToString());
        Assert.NotEqual(oldHash, stored!.PasswordHash);
    }

    [Fact]
    public async Task AdminAliasCannotElevateTeacherMembershipAtAnotherSchool()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var (email, id, _) = await CreateAccount(factory);
        var secondSchool = Guid.NewGuid();
        using (var scope = factory.Services.CreateScope())
        {
            using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("auth-test-fixture");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Schools.Add(new School { Id = secondSchool, Name = "Synthetic Second School", Slug = "synthetic-second" });
            var membership = new SchoolMembership { Id = Guid.NewGuid(), SchoolId = secondSchool, UserId = id, IsActive = true };
            membership.Roles.Add(new SchoolMembershipRole { MembershipId = membership.Id, Role = "Teacher" });
            db.SchoolMemberships.Add(membership);
            await db.SaveChangesAsync();
        }
        using var client = Client(factory);
        client.DefaultRequestHeaders.Authorization = new("Bearer", (await Login(client, email)).AccessToken);
        client.DefaultRequestHeaders.Add("X-School-Id", secondSchool.ToString());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(client, "/api/school-years",
            new { name = "Synthetic year", startDate = "2030-09-01", endDate = "2031-06-30" })).StatusCode);
    }

    [SchoolYearPostgresTests.PostgresFact]
    public async Task PostgresLoginProvisioningAndRevocationUseRealIdentityAndMembershipStorage()
    {
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            using var factory = PostgresFactory(connection);
            var (_, _, school) = await CreateAccount(factory, "Teacher");
            using (var scope = factory.Services.CreateScope())
            {
                var provisioner = scope.ServiceProvider.GetRequiredService<AdminProvisioner>();
                await provisioner.ProvisionAsync("postgres-admin@synthetic.test", Password, school);
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                Assert.Contains(await db.AuditLogs.ToListAsync(), item => item.SystemOperation == "initial-admin-provisioning");
            }
            using var client = Client(factory);
            var login = await Login(client, "postgres-admin@synthetic.test");
            Assert.Contains("Admin", login.User.Roles);
            client.DefaultRequestHeaders.Authorization = new("Bearer", login.AccessToken);
            Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/classes")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await Post(client, "/api/auth/logout", null)).StatusCode);
            Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
        }, migrateTarget: null);
    }

    [SchoolYearPostgresTests.PostgresFact]
    public async Task PendingPostgresSchemaReturns503WithoutAutomaticallyMigrating()
    {
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            using var factory = PostgresFactory(connection);
            var (email, _, _) = await CreateAccount(factory);
            using var client = Client(factory);
            var response = await Post(client, "/api/auth/login", new { email, password = Password });
            Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
            using var scope = factory.Services.CreateScope();
            Assert.Contains("20261104080000_ClassroomIntegrity",
                await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.GetPendingMigrationsAsync());
        }, migrateTarget: "20261103032915_StandardizedAuditing");
    }

    private static WebApplicationFactory<Program> PostgresFactory(string connection) =>
        new CustomWebApplicationFactory { EnableDevelopmentSeed = false }.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection));
            }));

    [Fact]
    public void SigningConfigurationFailsClosed()
    {
        var services = new ServiceCollection();
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Jwt:SigningKey"] = "short" }).Build();
        services.AddJwtAuthentication(configuration);
        using var provider = services.BuildServiceProvider();
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => provider.GetRequiredService<JwtSettings>());
    }

    private static HttpClient Client(WebApplicationFactory<Program> factory) => factory.CreateClient(
        new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object? body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        return await client.SendAsync(request);
    }

    private static async Task<AccessTokenResponse> Login(HttpClient client, string email)
    {
        var response = await Post(client, "/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AccessTokenResponse>())!;
    }

    private static async Task<(string Email, Guid Id, Guid School)> CreateAccount(WebApplicationFactory<Program> factory, string role = "SchoolAdmin")
    {
        using var scope = factory.Services.CreateScope();
        using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("auth-test-fixture");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = $"auth-{Guid.NewGuid():N}@synthetic.test";
        var user = new ApplicationUser { UserName = email, Email = email, IsActive = true };
        Assert.True((await users.CreateAsync(user, Password)).Succeeded);
        var school = new School { Id = Guid.NewGuid(), Name = "Synthetic Auth School", Slug = $"auth-{Guid.NewGuid():N}" };
        db.Schools.Add(school);
        var membership = new SchoolMembership { Id = Guid.NewGuid(), UserId = user.Id, SchoolId = school.Id, IsActive = true };
        membership.Roles.Add(new SchoolMembershipRole { MembershipId = membership.Id, Role = role });
        db.SchoolMemberships.Add(membership);
        await db.SaveChangesAsync();
        return (email, user.Id, school.Id);
    }
}

[ApiController]
[Route("api/auth-test")]
public sealed class AuthRoleTestController : ControllerBase
{
    [HttpGet("admin"), Authorize(Roles = "Admin")]
    public IActionResult Admin() => Ok();
    [HttpGet("teacher"), Authorize(Roles = "Teacher")]
    public IActionResult Teacher() => Ok();
    [HttpGet("parent"), Authorize(Roles = "Parent")]
    public IActionResult Parent() => Ok();
}
