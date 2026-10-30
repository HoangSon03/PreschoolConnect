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

public class SchoolManagementTests
{
    private const string Password = "SchoolAdmin!23";

    [Fact]
    public async Task AdminCanCreateSchoolYearAndClassWithoutChoosingAnotherSchoolsYear()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, schoolId, otherSchoolYearId) = await CreateMemberAsync(factory, SchoolRoleNames.SchoolAdmin, createOtherSchoolYear: true);
        await SignInAsync(client, email);

        var yearResponse = await PostWithCsrfAsync(client, "/api/school-years", new
        {
            name = "2026-2027",
            startDate = new DateOnly(2026, 9, 1),
            endDate = new DateOnly(2027, 6, 30),
            schoolId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.Created, yearResponse.StatusCode);
        var year = await yearResponse.Content.ReadFromJsonAsync<SchoolYearResponse>();
        Assert.NotNull(year);
        Assert.Equal(schoolId, year!.schoolId);

        var classroomResponse = await PostWithCsrfAsync(client, "/api/classes", new
        {
            schoolYearId = year.id,
            name = "Sunflower"
        });
        Assert.Equal(HttpStatusCode.Created, classroomResponse.StatusCode);

        var crossSchoolResponse = await PostWithCsrfAsync(client, "/api/classes", new
        {
            schoolYearId = otherSchoolYearId,
            name = "Unauthorized"
        });
        Assert.Equal(HttpStatusCode.NotFound, crossSchoolResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/classes");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var classes = await listResponse.Content.ReadFromJsonAsync<ClassroomResponse[]>();
        Assert.NotNull(classes);
        Assert.Single(classes!);
        Assert.All(classes, item => Assert.Equal(schoolId, item.schoolId));
    }

    [Fact]
    public async Task TeacherCannotCreateSchoolYear()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, _, _) = await CreateMemberAsync(factory, SchoolRoleNames.Teacher);
        await SignInAsync(client, email);

        var response = await PostWithCsrfAsync(client, "/api/school-years", new
        {
            name = "2026-2027",
            startDate = new DateOnly(2026, 9, 1),
            endDate = new DateOnly(2027, 6, 30)
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SchoolYearRejectsInvalidDateRange()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, _, _) = await CreateMemberAsync(factory, SchoolRoleNames.SchoolAdmin);
        await SignInAsync(client, email);

        var response = await PostWithCsrfAsync(client, "/api/school-years", new
        {
            name = "Invalid dates",
            startDate = new DateOnly(2027, 6, 30),
            endDate = new DateOnly(2026, 9, 1)
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static HttpClient CreateClient(CustomWebApplicationFactory factory) =>
        factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true
        });

    private static async Task<HttpResponseMessage> PostWithCsrfAsync(HttpClient client, string path, object payload)
    {
        var tokenResponse = await client.GetAsync("/api/auth/csrf");
        tokenResponse.EnsureSuccessStatusCode();
        var tokens = await tokenResponse.Content.ReadFromJsonAsync<CsrfResponse>();
        Assert.NotNull(tokens);

        using var request = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(payload)
        };
        request.Headers.Add("X-CSRF-TOKEN", tokens!.requestToken);
        return await client.SendAsync(request);
    }

    private static async Task SignInAsync(HttpClient client, string email)
    {
        var response = await PostWithCsrfAsync(client, "/api/auth/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static async Task<(string Email, Guid SchoolId, Guid OtherSchoolYearId)> CreateMemberAsync(
        CustomWebApplicationFactory factory,
        string role,
        bool createOtherSchoolYear = false)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var email = $"school-admin-{Guid.NewGuid():N}@northside.test";
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = email,
            Email = email,
            FirstName = "Test",
            LastName = "User",
            IsActive = true,
            EmailConfirmed = true
        };
        var createResult = await userManager.CreateAsync(user, Password);
        Assert.True(createResult.Succeeded, string.Join("; ", createResult.Errors.Select(error => error.Description)));

        var school = new School { Id = Guid.NewGuid(), Name = "Managed Test School", Slug = $"managed-{Guid.NewGuid():N}" };
        db.Schools.Add(school);
        var membership = new SchoolMembership
        {
            Id = Guid.NewGuid(),
            SchoolId = school.Id,
            UserId = user.Id,
            IsActive = true
        };
        membership.Roles.Add(new SchoolMembershipRole { MembershipId = membership.Id, Role = role });
        db.SchoolMemberships.Add(membership);

        var otherSchoolYearId = Guid.Empty;
        if (createOtherSchoolYear)
        {
            var otherSchool = new School { Id = Guid.NewGuid(), Name = "Other Test School", Slug = $"other-{Guid.NewGuid():N}" };
            db.Schools.Add(otherSchool);
            var otherSchoolYear = new SchoolYear
            {
                Id = Guid.NewGuid(),
                SchoolId = otherSchool.Id,
                Name = "Other school year",
                StartDate = new DateOnly(2026, 9, 1),
                EndDate = new DateOnly(2027, 6, 30)
            };
            db.SchoolYears.Add(otherSchoolYear);
            otherSchoolYearId = otherSchoolYear.Id;
        }

        await db.SaveChangesAsync();
        return (email, school.Id, otherSchoolYearId);
    }

    private sealed class CsrfResponse
    {
        public string requestToken { get; set; } = string.Empty;
    }

    private sealed class SchoolYearResponse
    {
        public Guid id { get; set; }
        public Guid schoolId { get; set; }
    }

    private sealed class ClassroomResponse
    {
        public Guid id { get; set; }
        public Guid schoolId { get; set; }
    }
}
