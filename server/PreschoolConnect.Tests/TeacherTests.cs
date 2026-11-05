using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Application.Teachers;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;

namespace PreschoolConnect.Tests;

public sealed class TeacherTests
{
    private const string Password = "SyntheticTeacher!123";
    private static TeacherRequest Profile(Guid? user = null) => new("Synthetic Teacher", new(1995, 4, 20), "Nữ",
        "0900000000", "teacher@synthetic.test", "", "Đại học Sư phạm mầm non", 2017, "Synthetic University",
        5, "Synthetic certificate", new(2022, 8, 1), "Không xác định thời hạn", "Toàn thời gian", true, user);

    [Fact]
    public async Task AdminCrudAndAssignmentHistoryAreAuditedAndImmediatelyRevokeClassAccess()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        await Workflow(factory);
    }

    private static async Task Workflow(WebApplicationFactory<Program> factory)
    {
        var fixture = await Fixture(factory);
        using var admin = await Login(factory, fixture.AdminEmail);
        var create = await Post(admin, "/api/teachers", Profile(fixture.TeacherId));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var teacher = (await create.Content.ReadFromJsonAsync<TeacherDto>())!;
        Assert.StartsWith("GV-", teacher.Code);
        Assert.Equal(fixture.TeacherId, teacher.Profile.UserId);
        using var own = await Login(factory, fixture.TeacherEmail);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(own, $"/api/teachers/{teacher.Id}",
            Profile(fixture.TeacherId), HttpMethod.Put)).StatusCode);
        Assert.Single((await own.GetFromJsonAsync<TeacherDto[]>("/api/teachers"))!);
        Assert.Empty((await own.GetFromJsonAsync<JsonElement[]>("/api/classes"))!);
        var assigned = await Post(admin, $"/api/teachers/{teacher.Id}/assignments",
            new AssignmentRequest(fixture.ClassId, "Giáo viên chủ nhiệm"));
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        var assignment = Assert.Single((await assigned.Content.ReadFromJsonAsync<TeacherDto>())!.Assignments);
        Assert.Single((await own.GetFromJsonAsync<JsonElement[]>("/api/classes"))!);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(admin, $"/api/teachers/{teacher.Id}/assignments",
            new AssignmentRequest(fixture.ClassId, "Giáo viên chủ nhiệm"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(own, $"/api/teachers/{teacher.Id}/assignments",
            new AssignmentRequest(fixture.ClassId, "Giáo viên chủ nhiệm"))).StatusCode);
        var updated = await Post(admin, $"/api/teachers/{teacher.Id}", Profile(fixture.TeacherId) with { Name = "Updated Synthetic" }, HttpMethod.Put);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        Assert.Equal("Updated Synthetic", (await updated.Content.ReadFromJsonAsync<TeacherDto>())!.Profile.Name);
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, $"/api/teachers/{teacher.Id}/assignments/{assignment.Id}", null, HttpMethod.Delete)).StatusCode);
        Assert.Empty((await own.GetFromJsonAsync<JsonElement[]>("/api/classes"))!);
        Assert.Empty((await own.GetFromJsonAsync<TeacherDto>($"/api/teachers/{teacher.Id}"))!.Assignments);
        var retained = (await admin.GetFromJsonAsync<TeacherDto>($"/api/teachers/{teacher.Id}"))!;
        Assert.NotNull(Assert.Single(retained.Assignments).EndedAtUtc);
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, $"/api/teachers/{teacher.Id}/assignments",
            new AssignmentRequest(fixture.ClassId, "Giáo viên đồng phụ trách"))).StatusCode);
        var counts = await own.GetFromJsonAsync<JsonElement[]>("/api/school-years");
        Assert.Equal(1, Assert.Single(counts!).GetProperty("classCount").GetInt32());
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, $"/api/teachers/{teacher.Id}",
            Profile(fixture.TeacherId) with { IsActive = false }, HttpMethod.Put)).StatusCode);
        Assert.Empty((await own.GetFromJsonAsync<JsonElement[]>("/api/classes"))!);
        Assert.Equal(0, Assert.Single((await own.GetFromJsonAsync<JsonElement[]>("/api/school-years"))!).GetProperty("classCount").GetInt32());
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        Assert.Equal(2, await db.TeacherAssignments.IgnoreQueryFilters().CountAsync());
        var logs = await db.AuditLogs.Where(log => log.EntityId == teacher.Id).ToListAsync();
        Assert.Contains(logs, log => log.Action == "I" && log.ActorUserId == fixture.AdminId);
        Assert.Contains(logs, log => log.Action == "U" && log.ChangedFields.Contains("Name"));
        Assert.DoesNotContain(logs, log => log.ChangedFields.Contains("Synthetic Teacher") || log.ChangedFields.Contains("0900000000"));
    }

    [Fact]
    public async Task AuthorizationRejectsOtherSchoolParentOtherTeacherOverpostingAndMissingCsrf()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var fixture = await Fixture(factory);
        var other = await Fixture(factory);
        using var admin = await Login(factory, fixture.AdminEmail);
        using var otherAdmin = await Login(factory, other.AdminEmail);
        using var teacher = await Login(factory, fixture.TeacherEmail);
        using var parent = await Login(factory, fixture.ParentEmail);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/teachers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync("/api/teachers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.GetAsync("/api/teachers/accounts")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(teacher, "/api/teachers", Profile())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/teachers", Profile())).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, "/api/teachers", new { name = "Synthetic", schoolId = other.SchoolId })).StatusCode);
        var response = await Post(admin, "/api/teachers", Profile(fixture.TeacherId));
        var saved = (await response.Content.ReadFromJsonAsync<TeacherDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.GetAsync($"/api/teachers/{saved.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Post(otherAdmin, $"/api/teachers/{saved.Id}", Profile(), HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, $"/api/teachers/{saved.Id}/assignments",
            new AssignmentRequest(other.ClassId, "Giáo viên chủ nhiệm"))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, "/api/teachers", Profile(other.TeacherId))).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Post(admin, "/api/teachers", Profile(fixture.TeacherId))).StatusCode);
        var unlinked = (await (await Post(admin, "/api/teachers", Profile())).Content.ReadFromJsonAsync<TeacherDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/teachers/{unlinked.Id}")).StatusCode);
        using var forged = new HttpRequestMessage(HttpMethod.Get, "/api/teachers");
        forged.Headers.Add("X-School-Id", other.SchoolId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await teacher.SendAsync(forged)).StatusCode);
        var accounts = (await admin.GetFromJsonAsync<TeacherAccountDto[]>("/api/teachers/accounts"))!;
        Assert.Contains(accounts, account => account.UserId == fixture.TeacherId);
        Assert.DoesNotContain(accounts, account => account.UserId == other.TeacherId);
    }

    [Fact]
    public async Task AssignmentsRejectDeletedClassesInactiveProfilesAndRevokedMemberships()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var fixture = await Fixture(factory);
        using var admin = await Login(factory, fixture.AdminEmail);
        var saved = (await (await Post(admin, "/api/teachers", Profile(fixture.TeacherId))).Content.ReadFromJsonAsync<TeacherDto>())!;
        using (var scope = factory.Services.CreateScope())
        {
            using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("teacher-test-fixture");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Classrooms.Remove(await db.Classrooms.SingleAsync(row => row.Id == fixture.ClassId));
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, $"/api/teachers/{saved.Id}/assignments",
            new AssignmentRequest(fixture.ClassId, "Giáo viên chủ nhiệm"))).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Post(admin, $"/api/teachers/{saved.Id}", Profile(fixture.TeacherId) with { IsActive = false }, HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, $"/api/teachers/{saved.Id}/assignments",
            new AssignmentRequest(fixture.ClassId, "Giáo viên chủ nhiệm"))).StatusCode);
        using var own = await Login(factory, fixture.TeacherEmail);
        using (var scope = factory.Services.CreateScope())
        {
            using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("teacher-test-fixture");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.SchoolMemberships.SingleAsync(row => row.UserId == fixture.TeacherId)).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Forbidden, (await own.GetAsync("/api/teachers")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await own.GetAsync("/api/classes")).StatusCode);
    }

    [Theory]
    [InlineData("name")]
    [InlineData("phone")]
    [InlineData("email")]
    [InlineData("birthday")]
    [InlineData("degree")]
    [InlineData("graduated")]
    [InlineData("experience")]
    public async Task ValidationRejectsInvalidProfiles(string field)
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var fixture = await Fixture(factory);
        using var admin = await Login(factory, fixture.AdminEmail);
        var profile = field switch {
            "name" => Profile() with { Name = " " }, "phone" => Profile() with { Phone = "123" },
            "email" => Profile() with { Email = "invalid" }, "birthday" => Profile() with { Birthday = new(2099, 1, 1) },
            "degree" => Profile() with { Degree = "unknown" }, "graduated" => Profile() with { Graduated = 2100 },
            _ => Profile() with { Experience = -1 }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await Post(admin, "/api/teachers", profile)).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Teachers.ToListAsync());
    }

    [Fact]
    public void MigrationHasNoModelDriftAndIncludesHistoryAndTenantGuards()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("20261104080000_ClassroomIntegrity");
        Assert.Contains("CK_TeacherAssignments_History", script);
        Assert.Contains("FK_TeacherAssignments_Classrooms_SchoolId_ClassroomId", script);
        Assert.Contains("TR_Business_Audit", script);
    }

    [SchoolYearPostgresTests.PostgresFact]
    public async Task ConcurrentAssignmentsCannotCreateDuplicateActivePairs()
    {
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            using var factory = PostgresFactory(connection);
            var fixture = await Fixture(factory);
            using var first = await Login(factory, fixture.AdminEmail);
            using var second = await Login(factory, fixture.AdminEmail);
            var teacher = (await (await Post(first, "/api/teachers", Profile())).Content.ReadFromJsonAsync<TeacherDto>())!;
            var payload = new AssignmentRequest(fixture.ClassId, "Giáo viên chủ nhiệm");
            var results = await Task.WhenAll(Post(first, $"/api/teachers/{teacher.Id}/assignments", payload),
                Post(second, $"/api/teachers/{teacher.Id}/assignments", payload));
            Assert.Single(results, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, response => response.StatusCode == HttpStatusCode.Conflict);
            using var scope = factory.Services.CreateScope();
            Assert.Equal(1, await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().TeacherAssignments.CountAsync());
        }, migrateTarget: null);
    }

    [SchoolYearPostgresTests.PostgresFact]
    public async Task RealPostgresWorkflowAndRawGuardsPreserveHistory()
    {
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            using var factory = PostgresFactory(connection);
            await Workflow(factory);
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            await using var command = new NpgsqlCommand("""DELETE FROM "Teachers" """, sql);
            var failure = await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync());
            Assert.Equal("CK_Business_NoPhysicalDelete", failure.ConstraintName);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ended = await db.TeacherAssignments.FirstAsync(row => row.EndedAtUtc != null);
            await using var mutate = new NpgsqlCommand("""
                UPDATE "TeacherAssignments" SET "EndedAtUtc" = NULL, "LastAction" = 'U',
                  "LastActionAtUtc" = "LastActionAtUtc" + interval '1 second' WHERE "Id" = @id
                """, sql);
            mutate.Parameters.AddWithValue("id", ended.Id);
            Assert.Equal("CK_TeacherAssignments_History", (await Assert.ThrowsAsync<PostgresException>(() => mutate.ExecuteNonQueryAsync())).ConstraintName);
        }, migrateTarget: null);
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

    private sealed record FixtureData(Guid SchoolId, Guid AdminId, Guid TeacherId, Guid ClassId,
        string AdminEmail, string TeacherEmail, string ParentEmail);

    private static async Task<FixtureData> Fixture(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("teacher-test-fixture");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var school = new School { Id = Guid.NewGuid(), Name = "Synthetic Teacher School", Slug = $"teacher-{Guid.NewGuid():N}" };
        db.Schools.Add(school);
        var result = new List<ApplicationUser>();
        foreach (var role in new[] { "SchoolAdmin", "Teacher", "Parent" })
        {
            var email = $"{role}-{Guid.NewGuid():N}@synthetic.test";
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, IsActive = true };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            result.Add(user);
            var member = new SchoolMembership { Id = Guid.NewGuid(), SchoolId = school.Id, UserId = user.Id, IsActive = true };
            member.Roles.Add(new SchoolMembershipRole { MembershipId = member.Id, Role = role });
            db.SchoolMemberships.Add(member);
        }
        var year = new SchoolYear { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Synthetic Year", StartDate = new(2026, 9, 1), EndDate = new(2027, 6, 30) };
        var classroom = new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "Synthetic Class" };
        db.SchoolYears.Add(year); db.Classrooms.Add(classroom);
        await db.SaveChangesAsync();
        return new(school.Id, result[0].Id, result[1].Id, classroom.Id, result[0].Email!, result[1].Email!, result[2].Email!);
    }

    private static async Task<HttpClient> Login(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var response = await Post(client, "/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", payload.GetProperty("accessToken").GetString());
        return client;
    }

    private static async Task<HttpResponseMessage> Post(HttpClient client, string path, object? body, HttpMethod? method = null)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        return await client.SendAsync(request);
    }
}
