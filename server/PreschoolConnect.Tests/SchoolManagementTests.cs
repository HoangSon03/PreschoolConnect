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

        var ownershipResponse = await PostWithCsrfAsync(client, "/api/school-years", new
        {
            name = "2026-2027",
            startDate = new DateOnly(2026, 9, 1),
            endDate = new DateOnly(2027, 6, 30),
            schoolId = Guid.NewGuid()
        });

        Assert.Equal(HttpStatusCode.BadRequest, ownershipResponse.StatusCode);
        var yearResponse = await PostWithCsrfAsync(client, "/api/school-years", new
        {
            name = "2026-2027", startDate = new DateOnly(2026, 9, 1), endDate = new DateOnly(2027, 6, 30)
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

    [Fact]
    public async Task AdminCanEditAndDeleteUnusedYear()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, _, _) = await CreateMemberAsync(factory, SchoolRoleNames.SchoolAdmin);
        await SignInAsync(client, email);
        var year = await CreateYearAsync(client);
        var update = await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/school-years/{year.id}",
            new { name = " Updated Year ", startDate = "2026-09-02", endDate = "2027-05-31" });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var list = await client.GetFromJsonAsync<SchoolYearResponse[]>("/api/school-years");
        Assert.Equal("Updated Year", Assert.Single(list!).name);
        Assert.Equal(0, list![0].classCount);
        Assert.Equal(HttpStatusCode.NoContent, (await SendWithCsrfAsync(client, HttpMethod.Delete, $"/api/school-years/{year.id}")).StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<SchoolYearResponse[]>("/api/school-years"))!);
        var deleted = await client.GetFromJsonAsync<SchoolYearResponse[]>("/api/admin/school-years/deleted");
        Assert.Equal(year.id, Assert.Single(deleted!).id);
        Assert.Equal(year.id, Assert.Single((await client.GetFromJsonAsync<SchoolYearResponse[]>("/api/admin/deleted-records/school-years"))!).id);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var retained = await db.SchoolYears.IgnoreQueryFilters().SingleAsync(item => item.Id == year.id);
            var userId = (await db.Users.SingleAsync(item => item.Email == email)).Id;
            Assert.Equal("D", retained.LastAction);
            Assert.Equal(userId, retained.CreatedByUserId);
            Assert.Equal(userId, retained.LastActionByUserId);
            Assert.Equal(new[] { "I", "U", "D" }, await db.AuditLogs.Where(log => log.EntityId == year.id)
                .OrderBy(log => log.OccurredAtUtc).Select(log => log.Action).ToArrayAsync());
        }
        Assert.Equal(HttpStatusCode.NotFound, (await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/school-years/{year.id}",
            new { name = "Restore attempt", startDate = "2026-09-02", endDate = "2027-05-31" })).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendWithCsrfAsync(client, HttpMethod.Delete, $"/api/school-years/{year.id}")).StatusCode);
    }

    [Fact]
    public async Task AdministrativeHistoryRequiresOwnSchoolAdminMembership()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, schoolId, _) = await CreateMemberAsync(factory, SchoolRoleNames.SchoolAdmin);
        await SignInAsync(client, email);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/admin/audit-logs")).StatusCode);
        using var forged = new HttpRequestMessage(HttpMethod.Get, "/api/admin/audit-logs");
        forged.Headers.Add("X-School-Id", Guid.NewGuid().ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.SendAsync(forged)).StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var user = await db.Users.SingleAsync(item => item.Email == email);
        var member = await db.SchoolMemberships.Include(item => item.Roles).SingleAsync(item => item.UserId == user.Id && item.SchoolId == schoolId);
        using var system = scope.ServiceProvider.GetRequiredService<PreschoolConnect.Application.Auth.AuditActorContext>().BeginSystemOperation("synthetic-role-change");
        db.SchoolMembershipRoles.RemoveRange(member.Roles);
        member.Roles.Add(new SchoolMembershipRole { MembershipId = member.Id, Role = SchoolRoleNames.Teacher });
        await db.SaveChangesAsync();
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/audit-logs")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/school-years/deleted")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/deleted-records/students")).StatusCode);
    }

    [Fact]
    public async Task ClientsCannotSupplyAuditOrOwnershipMetadata()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, _, _) = await CreateMemberAsync(factory, SchoolRoleNames.SchoolAdmin);
        await SignInAsync(client, email);
        var response = await PostWithCsrfAsync(client, "/api/school-years", new
        {
            name = "Synthetic rejected year", startDate = "2026-09-01", endDate = "2027-06-30",
            lastAction = "D", createdByUserId = Guid.NewGuid(), createdAtUtc = "1900-01-01T00:00:00Z"
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Empty((await client.GetFromJsonAsync<SchoolYearResponse[]>("/api/school-years"))!);
    }

    [Fact]
    public async Task UsedYearAllowsRenameButNotDateChangesOrDelete()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, _, _) = await CreateMemberAsync(factory, SchoolRoleNames.SchoolAdmin);
        await SignInAsync(client, email);
        var year = await CreateYearAsync(client);
        Assert.Equal(HttpStatusCode.Created, (await PostWithCsrfAsync(client, "/api/classes", new { name = "Test class", schoolYearId = year.id })).StatusCode);
        Assert.Equal(1, Assert.Single((await client.GetFromJsonAsync<SchoolYearResponse[]>("/api/school-years"))!).classCount);
        Assert.Equal(HttpStatusCode.OK, (await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/school-years/{year.id}",
            new { name = "Renamed year", startDate = "2026-09-01", endDate = "2027-06-30" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/school-years/{year.id}",
            new { name = "Bad edit", startDate = "2026-09-02", endDate = "2027-06-30" })).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await SendWithCsrfAsync(client, HttpMethod.Delete, $"/api/school-years/{year.id}")).StatusCode);
        Assert.Single((await client.GetFromJsonAsync<ClassroomResponse[]>("/api/classes"))!);
        Assert.Equal("Renamed year", Assert.Single((await client.GetFromJsonAsync<SchoolYearResponse[]>("/api/school-years"))!).name);
    }

    [Theory]
    [InlineData(" test year ", "2027-09-01", "2028-06-30")]
    [InlineData("Different", "2026-10-01", "2027-05-31")]
    [InlineData("Different", "2027-06-30", "2028-06-30")]
    public async Task CreateAndUpdateRejectDuplicateNameOrInclusiveOverlap(string name, string start, string end)
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, _, _) = await CreateMemberAsync(factory, SchoolRoleNames.SchoolAdmin);
        await SignInAsync(client, email);
        await CreateYearAsync(client);
        Assert.Equal(HttpStatusCode.Conflict, (await PostWithCsrfAsync(client, "/api/school-years", new { name, startDate = start, endDate = end })).StatusCode);
        var second = await PostWithCsrfAsync(client, "/api/school-years",
            new { name = "Next year", startDate = "2027-07-01", endDate = "2028-06-30" });
        Assert.Equal(HttpStatusCode.Created, second.StatusCode);
        var year = (await second.Content.ReadFromJsonAsync<SchoolYearResponse>())!;
        Assert.Equal(HttpStatusCode.Conflict, (await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/school-years/{year.id}",
            new { name, startDate = start, endDate = end })).StatusCode);
    }

    [Fact]
    public async Task UpdateAndDeleteRequireAdminCsrfAndSameSchool()
    {
        using var factory = new CustomWebApplicationFactory();
        using var client = CreateClient(factory);
        var (email, _, otherId) = await CreateMemberAsync(factory, SchoolRoleNames.SchoolAdmin, createOtherSchoolYear: true);
        await SignInAsync(client, email);
        var year = await CreateYearAsync(client);
        var body = new { name = "Valid edit", startDate = "2026-09-01", endDate = "2027-06-30" };
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PutAsJsonAsync($"/api/school-years/{year.id}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.DeleteAsync($"/api/school-years/{year.id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/school-years/{otherId}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await SendWithCsrfAsync(client, HttpMethod.Delete, $"/api/school-years/{otherId}")).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/school-years/{year.id}",
            new { name = "", startDate = "2026-09-01", endDate = "2026-09-01" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await PostWithCsrfAsync(client, "/api/school-years", new { name = "Missing start", endDate = "2028-06-30" })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await SendWithCsrfAsync(client, HttpMethod.Put, $"/api/school-years/{year.id}",
            new { name = "Missing start", endDate = "2028-06-30" })).StatusCode);
        var (teacherEmail, _, _) = await CreateMemberAsync(factory, SchoolRoleNames.Teacher);
        using var teacher = CreateClient(factory);
        await SignInAsync(teacher, teacherEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendWithCsrfAsync(teacher, HttpMethod.Put, $"/api/school-years/{year.id}", body)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await SendWithCsrfAsync(teacher, HttpMethod.Delete, $"/api/school-years/{year.id}")).StatusCode);
        using var anonymous = CreateClient(factory);
        Assert.Equal(HttpStatusCode.Unauthorized, (await SendWithCsrfAsync(anonymous, HttpMethod.Delete, $"/api/school-years/{year.id}")).StatusCode);
    }

    private static async Task<SchoolYearResponse> CreateYearAsync(HttpClient client)
    {
        var response = await PostWithCsrfAsync(client, "/api/school-years",
            new { name = "Test Year", startDate = "2026-09-01", endDate = "2027-06-30" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<SchoolYearResponse>())!;
    }

    private static async Task<HttpResponseMessage> SendWithCsrfAsync(HttpClient client, HttpMethod method, string path, object? payload = null)
    {
        var tokens = await client.GetFromJsonAsync<CsrfResponse>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path);
        if (payload is not null) request.Content = JsonContent.Create(payload);
        request.Headers.Add("X-CSRF-TOKEN", tokens!.requestToken);
        return await client.SendAsync(request);
    }

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
        using var systemOperation = scope.ServiceProvider.GetRequiredService<PreschoolConnect.Application.Auth.AuditActorContext>().BeginSystemOperation("school-test-fixture");
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
        public string name { get; set; } = string.Empty;
        public int classCount { get; set; }
    }

    private sealed class ClassroomResponse
    {
        public Guid id { get; set; }
        public Guid schoolId { get; set; }
    }
}
