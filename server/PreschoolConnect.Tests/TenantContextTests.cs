using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;

namespace PreschoolConnect.Tests;

public class TenantContextTests : IClassFixture<CustomWebApplicationFactory>
{
    private const string Password = "TenantPass!23";
    private readonly CustomWebApplicationFactory _factory;

    public TenantContextTests(CustomWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task TenantContext_RequiresAuthentication()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/tenant/context");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task TenantContext_ResolvesOnlyAnActiveMembershipForTheAuthenticatedUser()
    {
        var email = $"tenant-admin-{Guid.NewGuid():N}@northside.test";
        var (schoolId, unassignedSchoolId) = await EnsureTenantFixtureAsync(email);
        using var client = CreateClient();
        await SignInAsync(client, email);

        var contextResponse = await client.GetAsync("/api/tenant/context");
        Assert.Equal(HttpStatusCode.OK, contextResponse.StatusCode);
        var context = await contextResponse.Content.ReadFromJsonAsync<TenantContextResponse>();
        Assert.NotNull(context);
        Assert.Equal(schoolId, context!.schoolId);
        Assert.Equal(new[] { SchoolRoleNames.SchoolAdmin }, context.roles);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenant/context");
        request.Headers.Add("X-School-Id", unassignedSchoolId.ToString());
        var forbiddenResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    [Fact]
    public async Task TenantContext_RequiresExplicitSelectionWhenUserHasMultipleMemberships()
    {
        var email = $"tenant-admin-{Guid.NewGuid():N}@northside.test";
        var (schoolId, additionalSchoolId) = await EnsureTenantFixtureAsync(email, additionalMembership: true);
        using var client = CreateClient();
        await SignInAsync(client, email);

        var ambiguousResponse = await client.GetAsync("/api/tenant/context");
        Assert.Equal(HttpStatusCode.Conflict, ambiguousResponse.StatusCode);

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/tenant/context");
        request.Headers.Add("X-School-Id", additionalSchoolId.ToString());
        var selectedResponse = await client.SendAsync(request);
        Assert.Equal(HttpStatusCode.OK, selectedResponse.StatusCode);
        var context = await selectedResponse.Content.ReadFromJsonAsync<TenantContextResponse>();
        Assert.NotNull(context);
        Assert.Equal(additionalSchoolId, context!.schoolId);
        Assert.NotEqual(schoolId, context.schoolId);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    });

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var tokenResponse = await client.GetAsync("/api/auth/csrf");
        tokenResponse.EnsureSuccessStatusCode();
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        Assert.NotNull(tokens);

        using var loginRequest = new HttpRequestMessage(HttpMethod.Post, "/api/auth/login")
        {
            Content = JsonContent.Create(new { email, password = Password })
        };
        loginRequest.Headers.Add("X-CSRF-TOKEN", tokens!.requestToken);
        var loginResponse = await client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.OK, loginResponse.StatusCode);
    }

    private async Task<(Guid SchoolId, Guid UnassignedSchoolId)> EnsureTenantFixtureAsync(string email, bool additionalMembership = false)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);
        if (user is null)
        {
            user = new ApplicationUser
            {
                UserName = email,
                Email = email,
                FirstName = "Test",
                LastName = "Administrator",
                IsActive = true,
                EmailConfirmed = true
            };
            var createResult = await userManager.CreateAsync(user, Password);
            Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));
        }

        var primarySchool = await GetOrCreateSchoolAsync(db, "tenant-primary", "Primary Test School");
        var unassignedSchool = await GetOrCreateSchoolAsync(db, "tenant-unassigned", "Unassigned Test School");
        var membership = await db.SchoolMemberships
            .Include(item => item.Roles)
            .SingleOrDefaultAsync(item => item.UserId == user.Id && item.SchoolId == primarySchool.Id);
        if (membership is null)
        {
            membership = new SchoolMembership
            {
                Id = Guid.NewGuid(),
                SchoolId = primarySchool.Id,
                UserId = user.Id,
                IsActive = true
            };
            membership.Roles.Add(new SchoolMembershipRole
            {
                MembershipId = membership.Id,
                Role = SchoolRoleNames.SchoolAdmin
            });
            db.SchoolMemberships.Add(membership);
        }

        if (additionalMembership)
        {
            var additionalSchool = await GetOrCreateSchoolAsync(db, "tenant-additional", "Additional Test School");
            if (!await db.SchoolMemberships.AnyAsync(item => item.UserId == user.Id && item.SchoolId == additionalSchool.Id))
            {
                var additional = new SchoolMembership
                {
                    Id = Guid.NewGuid(),
                    SchoolId = additionalSchool.Id,
                    UserId = user.Id,
                    IsActive = true
                };
                additional.Roles.Add(new SchoolMembershipRole
                {
                    MembershipId = additional.Id,
                    Role = SchoolRoleNames.Teacher
                });
                db.SchoolMemberships.Add(additional);
            }

            await db.SaveChangesAsync();
            return (primarySchool.Id, additionalSchool.Id);
        }

        await db.SaveChangesAsync();
        return (primarySchool.Id, unassignedSchool.Id);
    }

    private static async Task<School> GetOrCreateSchoolAsync(ApplicationDbContext db, string slug, string name)
    {
        var school = await db.Schools.SingleOrDefaultAsync(item => item.Slug == slug);
        if (school is not null)
        {
            return school;
        }

        school = new School { Id = Guid.NewGuid(), Slug = slug, Name = name, IsActive = true };
        db.Schools.Add(school);
        return school;
    }

    private sealed class TenantContextResponse
    {
        public Guid schoolId { get; set; }
        public Guid membershipId { get; set; }
        public string[] roles { get; set; } = [];
    }

    private sealed class CsrfResponse
    {
        public string requestToken { get; set; } = string.Empty;
    }
}
