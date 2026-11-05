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
using PreschoolConnect.Application.Students;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;

namespace PreschoolConnect.Tests;

public sealed class StudentTests
{
    private const string Password = "SyntheticStudent!123";
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Ho_Chi_Minh").DateTime);
    private static StudentProfile Profile() => new("Synthetic Child", Today.AddYears(-4), "Nam", "Synthetic address",
        "Synthetic Guardian", "Bố", "0900000000", "guardian@synthetic.test", "Synthetic pickup", "0900000001", 97.5m, 14.8m, "Synthetic care text");
    private static EnrollmentRequest Enrollment(Guid classroom, DateOnly? start = null) =>
        new(classroom, start ?? Today.AddDays(-2), "Mầm · 3–4 tuổi", "Bán trú");

    [Fact]
    public async Task AdminProfileTransferParentHistoryAndTeacherRevocation() =>
        await Workflow(new CustomWebApplicationFactory { EnableDevelopmentSeed = false });

    private static async Task Workflow(WebApplicationFactory<Program> factory)
    {
        using (factory)
        {
            var fixture = await Fixture(factory);
            using var admin = await Login(factory, fixture.AdminEmail);
            using var teacher = await Login(factory, fixture.TeacherEmail);
            using var parent = await Login(factory, fixture.ParentEmail);
            var created = await Send(admin, "/api/students", new StudentRequest(Profile(), Enrollment(fixture.ClassId)));
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var student = (await created.Content.ReadFromJsonAsync<StudentDto>())!;
            Assert.NotEqual(Guid.Empty, student.Id);
            Assert.Equal("Đang theo học", student.Status);
            Assert.Equal(Today, student.Today);
            Assert.Equal(Today.AddDays(31), Assert.Single(student.Enrollments).EndDate);
            Assert.Single((await teacher.GetFromJsonAsync<StudentDto[]>("/api/students"))!);
            Assert.Empty((await parent.GetFromJsonAsync<StudentDto[]>("/api/students"))!);
            using (var scope = factory.Services.CreateScope())
            {
                using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("student-test-mapping");
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.StudentAccounts.Add(new StudentAccount { Id = Guid.NewGuid(), SchoolId = fixture.SchoolId, StudentId = student.Id, UserId = fixture.ParentId });
                await db.SaveChangesAsync();
            }
            Assert.Single((await parent.GetFromJsonAsync<StudentDto[]>("/api/students"))!);
            var updated = await Send(admin, $"/api/students/{student.Id}",
                new StudentRequest(Profile() with { Name = "Updated Synthetic Child", Care = "Updated synthetic care" }, null), HttpMethod.Put);
            Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
            Assert.Equal("Updated Synthetic Child", (await updated.Content.ReadFromJsonAsync<StudentDto>())!.Profile.Name);
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, $"/api/students/{student.Id}/enrollments",
                Enrollment(fixture.SecondClassId, Today.AddDays(-1)))).StatusCode);
            var transferred = await Send(admin, $"/api/students/{student.Id}/enrollments", Enrollment(fixture.SecondClassId, Today));
            Assert.Equal(HttpStatusCode.OK, transferred.StatusCode);
            var history = (await transferred.Content.ReadFromJsonAsync<StudentDto>())!.Enrollments;
            Assert.Equal(2, history.Length);
            Assert.Equal(Today, history.Single(row => row.ClassroomId == fixture.ClassId).EndDate);
            Assert.Equal(Today, history.Single(row => row.ClassroomId == fixture.SecondClassId).StartDate);
            Assert.Empty((await teacher.GetFromJsonAsync<StudentDto[]>("/api/students"))!);
            Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/students/{student.Id}")).StatusCode);
            Assert.Equal(2, (await parent.GetFromJsonAsync<StudentDto>($"/api/students/{student.Id}"))!.Enrollments.Length);
            var current = history.Single(row => row.ClassroomId == fixture.SecondClassId);
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, $"/api/students/{student.Id}/enrollments/{current.Id}", null, HttpMethod.Delete)).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await Send(parent, $"/api/students/{student.Id}", new StudentRequest(Profile(), null), HttpMethod.Put)).StatusCode);
            using var auditScope = factory.Services.CreateScope();
            var auditDb = auditScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.Equal(2, await auditDb.StudentInClasses.IgnoreQueryFilters().CountAsync());
            var logs = await auditDb.AuditLogs.Where(row => row.EntityId == student.Id).ToArrayAsync();
            Assert.Contains(logs, row => row.Action == "I" && row.ActorUserId == fixture.AdminId);
            Assert.Contains(logs, row => row.Action == "U" && row.ChangedFields.Contains("LastName"));
            Assert.DoesNotContain(logs, row => row.ChangedFields.Contains("synthetic care") || row.ChangedFields.Contains("0900000000"));
        }
    }

    [Fact]
    public async Task AuthorizationIsScopedAndRejectsOwnershipOverposting()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var fixture = await Fixture(factory);
        var other = await Fixture(factory);
        using var admin = await Login(factory, fixture.AdminEmail);
        using var otherAdmin = await Login(factory, other.AdminEmail);
        using var teacher = await Login(factory, fixture.TeacherEmail);
        using var parent = await Login(factory, fixture.ParentEmail);
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/students")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(teacher, "/api/students", new StudentRequest(Profile(), null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PostAsJsonAsync("/api/students", new StudentRequest(Profile(), null))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, "/api/students", new { profile = Profile(), schoolId = other.SchoolId })).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, "/api/students", new {
            profile = new { name = "Synthetic", birthday = Today.AddYears(-4), schoolId = other.SchoolId } })).StatusCode);
        var response = await Send(admin, "/api/students", new StudentRequest(Profile(), null));
        var student = (await response.Content.ReadFromJsonAsync<StudentDto>())!;
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.GetAsync($"/api/students/{student.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await parent.GetAsync($"/api/students/{student.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/students/{student.Id}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Send(otherAdmin, $"/api/students/{student.Id}", new StudentRequest(Profile(), null), HttpMethod.Put)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, $"/api/students/{student.Id}/enrollments", Enrollment(other.ClassId))).StatusCode);
        using var forged = new HttpRequestMessage(HttpMethod.Get, "/api/students");
        forged.Headers.Add("X-School-Id", other.SchoolId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.SendAsync(forged)).StatusCode);
    }

    [Theory]
    [InlineData("name")] [InlineData("birthday")] [InlineData("phone")] [InlineData("email")]
    [InlineData("guardian")] [InlineData("height")] [InlineData("weight")] [InlineData("precision")]
    [InlineData("care")] [InlineData("gender")] [InlineData("emergency")] [InlineData("relationship")]
    public async Task InvalidProfilesNeverWrite(string field)
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var fixture = await Fixture(factory);
        using var admin = await Login(factory, fixture.AdminEmail);
        var p = field switch {
            "name" => Profile() with { Name = "" }, "birthday" => Profile() with { Birthday = Today.AddDays(1) },
            "phone" => Profile() with { Phone = "123" }, "email" => Profile() with { Email = "invalid" },
            "guardian" => Profile() with { Guardian = " " }, "height" => Profile() with { Height = 251 },
            "weight" => Profile() with { Weight = -1 }, "precision" => Profile() with { Weight = 14.888m },
            "care" => Profile() with { Care = new string('x', 2001) }, "gender" => Profile() with { Gender = "unknown" },
            "emergency" => Profile() with { Emergency = "123" }, _ => Profile() with { Relationship = "unknown" }
        };
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, "/api/students", new StudentRequest(p, null))).StatusCode);
        using var scope = factory.Services.CreateScope();
        Assert.Empty(await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Students.ToArrayAsync());
    }

    [Fact]
    public async Task EnrollmentValidatesYearDatesAndKeepsClosedHistory()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var fixture = await Fixture(factory);
        using var admin = await Login(factory, fixture.AdminEmail);
        foreach (var request in new[] {
            Enrollment(fixture.ClassId, Today.AddDays(1)), Enrollment(fixture.ClassId, Today.AddDays(-31)),
            Enrollment(fixture.ClassId) with { AgeGroup = "unknown" }, Enrollment(fixture.ClassId) with { StudyMode = "unknown" },
            Enrollment(Guid.NewGuid()) })
            Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, "/api/students", new StudentRequest(Profile(), request))).StatusCode);
        var student = (await (await Send(admin, "/api/students", new StudentRequest(Profile(), null))).Content.ReadFromJsonAsync<StudentDto>())!;
        Assert.Equal("Chưa phân lớp", student.Status);
        var response = await Send(admin, $"/api/students/{student.Id}/enrollments", Enrollment(fixture.ClassId));
        var enrolled = (await response.Content.ReadFromJsonAsync<StudentDto>())!;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, $"/api/students/{student.Id}/enrollments", Enrollment(fixture.ClassId, Today))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await Send(admin, $"/api/students/{student.Id}",
            new StudentRequest(Profile() with { Birthday = Today }, null), HttpMethod.Put)).StatusCode);
        var enrollment = Assert.Single(enrolled.Enrollments);
        var ended = await Send(admin, $"/api/students/{student.Id}/enrollments/{enrollment.Id}", null, HttpMethod.Delete);
        Assert.Equal(HttpStatusCode.OK, ended.StatusCode);
        Assert.Equal("Đã nghỉ học", (await ended.Content.ReadFromJsonAsync<StudentDto>())!.Status);
        Assert.Equal(HttpStatusCode.Conflict, (await Send(admin, $"/api/students/{student.Id}/enrollments/{enrollment.Id}", null, HttpMethod.Delete)).StatusCode);
        var again = await Send(admin, $"/api/students/{student.Id}/enrollments", Enrollment(fixture.SecondClassId, Today));
        Assert.Equal(HttpStatusCode.OK, again.StatusCode);
        Assert.Equal(2, (await again.Content.ReadFromJsonAsync<StudentDto>())!.Enrollments.Length);
    }

    [Fact]
    public async Task TeacherAccessIsRevokedAndParentMappingNeverAuthorizesAnotherStudent()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        var fixture = await Fixture(factory);
        using var admin = await Login(factory, fixture.AdminEmail);
        using var teacher = await Login(factory, fixture.TeacherEmail);
        using var parent = await Login(factory, fixture.ParentEmail);
        var first = (await (await Send(admin, "/api/students", new StudentRequest(Profile(), Enrollment(fixture.ClassId)))).Content.ReadFromJsonAsync<StudentDto>())!;
        var other = (await (await Send(admin, "/api/students", new StudentRequest(Profile(), null))).Content.ReadFromJsonAsync<StudentDto>())!;
        using (var scope = factory.Services.CreateScope())
        {
            using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("student-test-revocation");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.StudentAccounts.Add(new StudentAccount { Id = Guid.NewGuid(), SchoolId = fixture.SchoolId, StudentId = first.Id, UserId = fixture.ParentId });
            (await db.Teachers.SingleAsync()).IsActive = false;
            await db.SaveChangesAsync();
        }
        Assert.Empty((await teacher.GetFromJsonAsync<StudentDto[]>("/api/students"))!);
        Assert.Equal(HttpStatusCode.NotFound, (await parent.GetAsync($"/api/students/{other.Id}")).StatusCode);
        using (var scope = factory.Services.CreateScope())
        {
            using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("student-test-unmap");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.Teachers.SingleAsync()).IsActive = true;
            db.StudentAccounts.Remove(await db.StudentAccounts.SingleAsync());
            await db.SaveChangesAsync();
        }
        Assert.Single((await teacher.GetFromJsonAsync<StudentDto[]>("/api/students"))!);
        Assert.Empty((await parent.GetFromJsonAsync<StudentDto[]>("/api/students"))!);
        using (var scope = factory.Services.CreateScope())
        {
            using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("student-test-end-assignment");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            (await db.TeacherAssignments.SingleAsync()).EndedAtUtc = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        Assert.Empty((await teacher.GetFromJsonAsync<StudentDto[]>("/api/students"))!);
    }

    [Fact]
    public async Task RetainedPastSchoolYearAllowsReenrollmentWithoutExposingOtherClassHistory()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        await PastYearWorkflow(factory);
    }

    private static async Task PastYearWorkflow(WebApplicationFactory<Program> factory)
    {
        var fixture = await Fixture(factory);
        Guid oldClassId;
        using (var scope = factory.Services.CreateScope())
        {
            using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("student-test-past-year");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var year = new SchoolYear { Id = Guid.NewGuid(), SchoolId = fixture.SchoolId, Name = "Synthetic Past Year",
                StartDate = Today.AddDays(-400), EndDate = Today.AddDays(-35) };
            var classroom = new Classroom { Id = Guid.NewGuid(), SchoolId = fixture.SchoolId, SchoolYearId = year.Id, Name = "Synthetic Past Class" };
            oldClassId = classroom.Id;
            db.SchoolYears.Add(year); db.Classrooms.Add(classroom); await db.SaveChangesAsync();
        }
        using var admin = await Login(factory, fixture.AdminEmail);
        using var teacher = await Login(factory, fixture.TeacherEmail);
        var created = await Send(admin, "/api/students", new StudentRequest(Profile(), Enrollment(oldClassId, Today.AddDays(-100))));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var student = (await created.Content.ReadFromJsonAsync<StudentDto>())!;
        Assert.Equal("Đã nghỉ học", student.Status);
        Assert.False(student.IsNew);
        var enrolled = await Send(admin, $"/api/students/{student.Id}/enrollments", Enrollment(fixture.ClassId, Today));
        Assert.Equal(HttpStatusCode.OK, enrolled.StatusCode);
        Assert.Equal(2, (await enrolled.Content.ReadFromJsonAsync<StudentDto>())!.Enrollments.Length);
        var visible = (await teacher.GetFromJsonAsync<StudentDto>($"/api/students/{student.Id}"))!;
        Assert.Equal(fixture.ClassId, Assert.Single(visible.Enrollments).ClassroomId);
        Assert.False(visible.IsNew);
        using var auditScope = factory.Services.CreateScope();
        using var system = auditScope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("student-test-retention");
        var auditDb = auditScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        auditDb.Students.Remove(await auditDb.Students.SingleAsync());
        await Assert.ThrowsAsync<AuditPolicyException>(() => auditDb.SaveChangesAsync());
    }

    [Fact]
    public void MigrationMatchesModelAndContainsTenantHistoryOverlapGuards()
    {
        using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=test").Options);
        Assert.False(db.Database.HasPendingModelChanges());
        var sql = db.GetService<IMigrator>().GenerateScript("20261104095259_TeacherProfiles");
        Assert.Contains("CREATE EXTENSION IF NOT EXISTS btree_gist", sql);
        Assert.Contains("EX_StudentInClasses_NoOverlap", sql);
        Assert.Contains("FK_StudentInClasses_Classrooms_SchoolId_ClassroomId", sql);
        Assert.Contains("CK_StudentInClasses_History", sql);
    }

    [SchoolYearPostgresTests.PostgresFact]
    public async Task PostgresWorkflowEnforcesActualQueriesAndHistory() =>
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection => await Workflow(PostgresFactory(connection)), migrateTarget: null);

    [SchoolYearPostgresTests.PostgresFact]
    public async Task PostgresPreservesPastYearHistoryAndTeacherVisibility() =>
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            using var factory = PostgresFactory(connection);
            await PastYearWorkflow(factory);
        }, migrateTarget: null);

    [SchoolYearPostgresTests.PostgresFact]
    public async Task ConcurrentTransfersCommitExactlyOneCompleteHistoryChange()
    {
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            using var factory = PostgresFactory(connection);
            var fixture = await Fixture(factory);
            using var first = await Login(factory, fixture.AdminEmail);
            using var second = await Login(factory, fixture.AdminEmail);
            var student = (await (await Send(first, "/api/students",
                new StudentRequest(Profile(), Enrollment(fixture.ClassId)))).Content.ReadFromJsonAsync<StudentDto>())!;
            var payload = Enrollment(fixture.SecondClassId, Today);
            var results = await Task.WhenAll(Send(first, $"/api/students/{student.Id}/enrollments", payload),
                Send(second, $"/api/students/{student.Id}/enrollments", payload));
            Assert.Single(results, row => row.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, row => row.StatusCode == HttpStatusCode.Conflict);
            var history = (await first.GetFromJsonAsync<StudentDto>($"/api/students/{student.Id}"))!.Enrollments;
            Assert.Equal(2, history.Length);
            Assert.Equal(Today, history.Single(row => row.ClassroomId == fixture.ClassId).EndDate);
            Assert.Equal(Today, history.Single(row => row.ClassroomId == fixture.SecondClassId).StartDate);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var ids = history.Select(row => row.Id).ToArray();
            Assert.Equal(3, await db.AuditLogs.CountAsync(row => ids.Contains(row.EntityId)));
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            await using var reopen = new NpgsqlCommand("""
                UPDATE "StudentInClasses" SET "EndDate" = @end, "LastAction" = 'U',
                  "LastActionAtUtc" = "LastActionAtUtc" + interval '1 second'
                WHERE "Id" = @id
                """, sql);
            reopen.Parameters.AddWithValue("end", Today.AddDays(31));
            reopen.Parameters.AddWithValue("id", history.Single(row => row.ClassroomId == fixture.ClassId).Id);
            Assert.Equal("CK_StudentInClasses_History", (await Assert.ThrowsAsync<PostgresException>(() => reopen.ExecuteNonQueryAsync())).ConstraintName);
        }, migrateTarget: null);
    }

    [SchoolYearPostgresTests.PostgresFact]
    public async Task ConcurrentEnrollmentsAndRawWritesCannotBypassHistory()
    {
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            using var factory = PostgresFactory(connection);
            var fixture = await Fixture(factory);
            using var first = await Login(factory, fixture.AdminEmail);
            using var second = await Login(factory, fixture.AdminEmail);
            var student = (await (await Send(first, "/api/students", new StudentRequest(Profile(), null))).Content.ReadFromJsonAsync<StudentDto>())!;
            var payload = Enrollment(fixture.ClassId, Today);
            var results = await Task.WhenAll(Send(first, $"/api/students/{student.Id}/enrollments", payload),
                Send(second, $"/api/students/{student.Id}/enrollments", payload));
            Assert.Single(results, row => row.StatusCode == HttpStatusCode.OK);
            Assert.Single(results, row => row.StatusCode == HttpStatusCode.Conflict);
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            await using var delete = new NpgsqlCommand("""DELETE FROM "StudentInClasses" """, sql);
            Assert.Equal("CK_Business_NoPhysicalDelete", (await Assert.ThrowsAsync<PostgresException>(() => delete.ExecuteNonQueryAsync())).ConstraintName);
            await using var history = new NpgsqlCommand("""
                UPDATE "StudentInClasses" SET "ClassroomId" = @class, "LastAction" = 'U',
                  "LastActionAtUtc" = "LastActionAtUtc" + interval '1 second'
                """, sql);
            history.Parameters.AddWithValue("class", fixture.SecondClassId);
            Assert.Equal("CK_StudentInClasses_History", (await Assert.ThrowsAsync<PostgresException>(() => history.ExecuteNonQueryAsync())).ConstraintName);
            await using var overlap = new NpgsqlCommand("""
                INSERT INTO "StudentInClasses" ("Id", "SchoolId", "StudentId", "ClassroomId", "StartDate", "EndDate",
                  "AgeGroup", "StudyMode", "CreatedAtUtc", "LastActionAtUtc", "LastAction")
                VALUES (@id, @school, @student, @class, @start, @end, 'Mầm · 3–4 tuổi', 'Bán trú', now(), now(), 'I')
                """, sql);
            overlap.Parameters.AddWithValue("id", Guid.NewGuid()); overlap.Parameters.AddWithValue("school", fixture.SchoolId);
            overlap.Parameters.AddWithValue("student", student.Id); overlap.Parameters.AddWithValue("class", fixture.SecondClassId);
            overlap.Parameters.AddWithValue("start", Today); overlap.Parameters.AddWithValue("end", Today.AddDays(31));
            Assert.Equal("EX_StudentInClasses_NoOverlap", (await Assert.ThrowsAsync<PostgresException>(() => overlap.ExecuteNonQueryAsync())).ConstraintName);
            foreach (var table in new[] { "Students", "Classrooms" })
            {
                await using var retained = new NpgsqlCommand($"""
                    UPDATE "{table}" SET "LastAction" = 'D', "LastActionAtUtc" = "LastActionAtUtc" + interval '1 second'
                    WHERE "Id" = @id
                    """, sql);
                retained.Parameters.AddWithValue("id", table == "Students" ? student.Id : fixture.ClassId);
                Assert.Equal("CK_StudentInClasses_RetainedParent", (await Assert.ThrowsAsync<PostgresException>(() => retained.ExecuteNonQueryAsync())).ConstraintName);
            }
            var otherSchool = await Fixture(factory);
            overlap.Parameters["class"].Value = otherSchool.ClassId;
            Assert.Equal("CK_StudentInClasses_Parents", (await Assert.ThrowsAsync<PostgresException>(() => overlap.ExecuteNonQueryAsync())).ConstraintName);
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

    private sealed record FixtureData(Guid SchoolId, Guid AdminId, Guid ParentId, Guid ClassId, Guid SecondClassId,
        string AdminEmail, string TeacherEmail, string ParentEmail);
    private static async Task<FixtureData> Fixture(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        using var operation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("student-test-fixture");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var school = new School { Id = Guid.NewGuid(), Name = "Synthetic Student School", Slug = $"student-{Guid.NewGuid():N}" };
        db.Schools.Add(school);
        var accounts = new List<ApplicationUser>();
        foreach (var role in new[] { "SchoolAdmin", "Teacher", "Parent" })
        {
            var email = $"{role}-{Guid.NewGuid():N}@synthetic.test";
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, IsActive = true };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            accounts.Add(user);
            var member = new SchoolMembership { Id = Guid.NewGuid(), SchoolId = school.Id, UserId = user.Id, IsActive = true };
            member.Roles.Add(new SchoolMembershipRole { MembershipId = member.Id, Role = role });
            db.SchoolMemberships.Add(member);
        }
        var year = new SchoolYear { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Synthetic Year", StartDate = Today.AddDays(-30), EndDate = Today.AddDays(30) };
        var classroom = new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "Synthetic Class" };
        var secondClass = new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "Other Synthetic Class" };
        db.SchoolYears.Add(year); db.Classrooms.AddRange(classroom, secondClass);
        var teacher = new Teacher { Id = Guid.NewGuid(), SchoolId = school.Id, UserId = accounts[1].Id, Name = "Synthetic Teacher",
            Birthday = Today.AddYears(-30), Gender = "Nữ", Phone = "0900000000", Email = accounts[1].Email!,
            Degree = "Synthetic degree", Graduated = 2017, University = "Synthetic University", StartDate = Today.AddYears(-1),
            Contract = "Synthetic contract", Work = "Synthetic work" };
        db.Teachers.Add(teacher);
        await db.SaveChangesAsync();
        db.TeacherAssignments.Add(new TeacherAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, TeacherId = teacher.Id,
            ClassroomId = classroom.Id, Role = "Giáo viên chủ nhiệm", StartedAtUtc = DateTimeOffset.UtcNow });
        await db.SaveChangesAsync();
        return new(school.Id, accounts[0].Id, accounts[2].Id, classroom.Id, secondClass.Id, accounts[0].Email!, accounts[1].Email!, accounts[2].Email!);
    }

    private static async Task<HttpClient> Login(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var response = await Send(client, "/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", payload.GetProperty("accessToken").GetString());
        return client;
    }
    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, object? body, HttpMethod? method = null)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method ?? HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        return await client.SendAsync(request);
    }
}
