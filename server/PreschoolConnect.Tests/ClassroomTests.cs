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
using PreschoolConnect.Application.Classrooms;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;

namespace PreschoolConnect.Tests;

public sealed class ClassroomTests
{
    private const string Password = "SyntheticClass!123";
    private static DateOnly Today => DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeBySystemTimeZoneId(DateTimeOffset.UtcNow, "Asia/Ho_Chi_Minh").DateTime);
    private sealed class FixedClock(int offsetDays) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new DateTimeOffset(Today.AddDays(offsetDays).ToDateTime(new(0, 15)), TimeSpan.FromHours(7)).ToUniversalTime();
    }

    [Fact]
    public async Task AdminEditsRealMetadataCountsRosterAndAuditsWithoutChangingYear()
    {
        using var factory = Factory();
        await Workflow(factory);
    }

    private static async Task Workflow(WebApplicationFactory<Program> factory)
    {
        var fixture = await Seed(factory);
        using var admin = await Login(factory, fixture.Admin);
        var list = (await admin.GetFromJsonAsync<ClassroomSummary[]>("/api/classes"))!;
        Assert.Equal(2, list.Length);
        var summary = list.Single(row => row.Id == fixture.ClassId);
        Assert.Equal(1, summary.StudentCount);
        Assert.Equal("Synthetic Teacher", Assert.Single(summary.Teachers).Name);
        var detail = (await admin.GetFromJsonAsync<ClassroomDetail>($"/api/classes/{fixture.ClassId}"))!;
        Assert.Equal(Today, detail.Today);
        Assert.Equal(summary.StudentCount, detail.Students.Length);
        Assert.Equal("Synthetic Child", Assert.Single(detail.Students).Name);
        var update = Update(detail);
        var response = await Put(admin, fixture.ClassId, update);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<ClassroomDetail>())!;
        Assert.Equal("Updated Synthetic Class", saved.Classroom.Name);
        Assert.Equal("Synthetic Room", saved.Classroom.Room);
        Assert.Equal(1, saved.Classroom.Capacity);
        Assert.Equal(fixture.YearId, saved.Classroom.SchoolYearId);
        Assert.Equal(1, saved.Classroom.StudentCount);
        Assert.Equal(2, saved.Schedule.Length);
        Assert.Equal("Synthetic weekly note", Assert.Single(saved.WeeklyNotes).Text);
        Assert.Equal(HttpStatusCode.Conflict, (await Put(admin, fixture.ClassId, update)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await Put(admin, fixture.ClassId,
            Update(saved) with { Name = " second synthetic class " })).StatusCode);
        using var scope = factory.Services.CreateScope();
        var logs = await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().AuditLogs
            .Where(row => row.EntityId == fixture.ClassId).ToArrayAsync();
        Assert.Contains(logs, row => row.Action == "U" && row.ChangedFields.Contains("ScheduleJson"));
        Assert.DoesNotContain(logs, row => row.ChangedFields.Contains("Synthetic weekly note") ||
            row.ChangedFields.Contains("Synthetic Room"));
    }

    [Fact]
    public async Task FutureEnrollmentsAreExcludedUsingTheServerSchoolDate()
    {
        using var factory = Factory(offsetDays: -1);
        var fixture = await Seed(factory);
        Guid futureStudent;
        using (var scope = factory.Services.CreateScope())
        {
            using var audit = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("future-class-fixture");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            futureStudent = await db.Students.Where(student => student.SchoolId == fixture.SchoolId &&
                !db.StudentInClasses.Any(enrollment => enrollment.StudentId == student.Id))
                .Select(student => student.Id).SingleAsync();
            db.StudentInClasses.Add(new StudentInClass { Id = Guid.NewGuid(), SchoolId = fixture.SchoolId,
                StudentId = futureStudent, ClassroomId = fixture.ClassId, StartDate = Today, EndDate = Today.AddDays(31),
                AgeGroup = "Mầm · 3–4 tuổi", StudyMode = "Bán trú" });
            await db.SaveChangesAsync();
        }
        using var admin = await Login(factory, fixture.Admin);
        var detail = (await admin.GetFromJsonAsync<ClassroomDetail>($"/api/classes/{fixture.ClassId}"))!;
        Assert.Equal(Today.AddDays(-1), detail.Today);
        Assert.Equal(2, detail.Classroom.StudentCount);
        Assert.DoesNotContain(detail.Students, student => student.Id == futureStudent);
    }

    [Fact]
    public async Task AccessIsSchoolAndAssignmentScopedAndRejectsOwnershipOverposting()
    {
        using var factory = Factory();
        var fixture = await Seed(factory);
        var other = await Seed(factory);
        using var admin = await Login(factory, fixture.Admin);
        using var otherAdmin = await Login(factory, other.Admin);
        using var teacher = await Login(factory, fixture.Teacher);
        using var parent = await Login(factory, fixture.Parent);
        using var anonymous = factory.CreateClient();
        var path = $"/api/classes/{fixture.ClassId}";
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await parent.GetAsync("/api/classes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherAdmin.GetAsync(path)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync($"/api/classes/{fixture.SecondClassId}")).StatusCode);
        Assert.Single((await teacher.GetFromJsonAsync<ClassroomSummary[]>("/api/classes"))!);
        var detail = (await teacher.GetFromJsonAsync<ClassroomDetail>(path))!;
        Assert.Single(detail.Students);
        Assert.Equal(HttpStatusCode.Forbidden, (await Put(teacher, fixture.ClassId, Update(detail))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Put(otherAdmin, fixture.ClassId, Update(detail))).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await admin.PutAsJsonAsync(path, Update(detail))).StatusCode);
        foreach (var payload in new object[] {
            new { name = "Synthetic", room = "", capacity = 1, schedule = Array.Empty<object>(), weeklyNotes = Array.Empty<object>(),
                lastActionAtUtc = detail.LastActionAtUtc, schoolId = other.SchoolId },
            new { name = "Synthetic", room = "", capacity = 1, schedule = Array.Empty<object>(), weeklyNotes = Array.Empty<object>(),
                lastActionAtUtc = detail.LastActionAtUtc, schoolYearId = other.YearId },
            new { name = "Synthetic", room = "", capacity = 1,
                schedule = new[] { new { start = "07:00:00", end = "08:00:00", activity = "Synthetic", teacherId = Guid.NewGuid() } },
                weeklyNotes = Array.Empty<object>(), lastActionAtUtc = detail.LastActionAtUtc }
        })
            Assert.Equal(HttpStatusCode.BadRequest, (await Put(admin, fixture.ClassId, payload)).StatusCode);
        using var forged = new HttpRequestMessage(HttpMethod.Get, path);
        forged.Headers.Add("X-School-Id", other.SchoolId.ToString());
        Assert.Equal(HttpStatusCode.Forbidden, (await admin.SendAsync(forged)).StatusCode);
        foreach (var change in new[] { "inactive", "ended", "deleted" })
        {
            using var scope = factory.Services.CreateScope();
            using var audit = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("class-revocation");
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var profile = await db.Teachers.SingleAsync(row => row.SchoolId == fixture.SchoolId);
            if (change == "inactive") profile.IsActive = false;
            else if (change == "ended")
            {
                profile.IsActive = true;
                (await db.TeacherAssignments.SingleAsync(row => row.SchoolId == fixture.SchoolId)).EndedAtUtc = DateTimeOffset.UtcNow;
            }
            else db.Teachers.Remove(profile);
            await db.SaveChangesAsync();
            Assert.Equal(HttpStatusCode.NotFound, (await teacher.GetAsync(path)).StatusCode);
            Assert.Empty((await teacher.GetFromJsonAsync<ClassroomSummary[]>("/api/classes"))!);
        }
    }

    [Theory]
    [InlineData("overlap")]
    [InlineData("capacity")]
    [InlineData("name")]
    [InlineData("room")]
    [InlineData("null")]
    [InlineData("null-row")]
    [InlineData("precision")]
    [InlineData("week")]
    [InlineData("duplicate-week")]
    [InlineData("too-many")]
    public void InvalidAggregateIsRejected(string field)
    {
        var request = new ClassroomUpdate("Synthetic", "", null, [], [], DateTimeOffset.UtcNow);
        var activity = new ClassroomActivity(new(7, 0), new(8, 0), "Synthetic");
        var note = new ClassroomNote(new(2026, 11, 2), "Synthetic");
        request = field switch {
            "overlap" => request with { Schedule = [activity, activity with { Start = new(7, 59) }] },
            "capacity" => request with { Capacity = 501 },
            "name" => request with { Name = "" },
            "room" => request with { Room = new string('x', 101) },
            "null" => request with { Schedule = null! },
            "null-row" => request with { Schedule = [null!] },
            "precision" => request with { Schedule = [activity with { Start = new(7, 0, 1) }] },
            "week" => request with { WeeklyNotes = [note with { WeekStart = new(2026, 11, 3) }] },
            "duplicate-week" => request with { WeeklyNotes = [note, note] },
            _ => request with { WeeklyNotes = Enumerable.Repeat(note, 53).ToArray() }
        };
        Assert.Equal(ClassroomFailure.Invalid, Assert.Throws<ClassroomException>(() =>
            ClassroomRules.Validate(request.Name, request.Room, request.Capacity, request.Schedule, request.WeeklyNotes)).Failure);
    }

    [SchoolYearPostgresTests.PostgresFact]
    public async Task MigrationPreservesExistingClassAndConcurrentEditsDoNotLoseData()
    {
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            var school = Guid.NewGuid(); var year = Guid.NewGuid(); var classroom = Guid.NewGuid();
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            await using (var seed = new NpgsqlCommand("""
                INSERT INTO "Schools" ("Id","Name","Slug","IsActive","CreatedUtc","CreatedAtUtc","LastActionAtUtc","LastAction")
                  VALUES (@school,'Synthetic legacy','synthetic-legacy',true,now(),now(),now(),'I');
                INSERT INTO "SchoolYears" ("Id","SchoolId","Name","StartDate","EndDate","CreatedAtUtc","LastActionAtUtc","LastAction")
                  VALUES (@year,@school,'Synthetic legacy year','2026-09-01','2027-06-30',now(),now(),'I');
                INSERT INTO "Classrooms" ("Id","SchoolId","SchoolYearId","Name","CreatedAtUtc","LastActionAtUtc","LastAction")
                  VALUES (@class,@school,@year,'Synthetic legacy class',now(),now(),'I');
                """, sql))
            {
                seed.Parameters.AddWithValue("school", school); seed.Parameters.AddWithValue("year", year);
                seed.Parameters.AddWithValue("class", classroom); await seed.ExecuteNonQueryAsync();
            }
            await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options))
            {
                await db.Database.MigrateAsync();
                var existing = await db.Classrooms.SingleAsync();
                Assert.Equal(classroom, existing.Id); Assert.Equal(year, existing.SchoolYearId);
                Assert.Equal("", existing.Room); Assert.Null(existing.Capacity);
                Assert.Equal("[]", existing.ScheduleJson); Assert.Equal("[]", existing.WeeklyNotesJson);
                Assert.Equal("I", existing.LastAction);
            }
            using var factory = Factory(connection);
            var fixture = await Seed(factory);
            using var first = await Login(factory, fixture.Admin);
            using var second = await Login(factory, fixture.Admin);
            var detail = (await first.GetFromJsonAsync<ClassroomDetail>($"/api/classes/{fixture.ClassId}"))!;
            var responses = await Task.WhenAll(Put(first, fixture.ClassId, Update(detail) with { Room = "Synthetic A" }),
                Put(second, fixture.ClassId, Update(detail) with { Room = "Synthetic B" }));
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        }, migrateTarget: "20261104102931_StudentProfilesAndEnrollment");
    }

    [SchoolYearPostgresTests.PostgresFact]
    public async Task LatestMigrationQueriesAuditAndRawConstraintsWorkOnPostgres()
    {
        await SchoolYearPostgresTests.WithDatabaseAsync(async connection =>
        {
            using var factory = Factory(connection);
            await Workflow(factory);
            using var scope = factory.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            Assert.False(db.Database.HasPendingModelChanges());
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            foreach (var statement in new[] {
                """UPDATE "Classrooms" SET "Capacity" = 501, "LastAction" = 'U', "LastActionAtUtc" = "LastActionAtUtc" + interval '1 second'""",
                """UPDATE "Classrooms" SET "ScheduleJson" = '[{"Start":"07:00:00","End":"08:00:00","Activity":"Synthetic"},{"Start":"07:30:00","End":"09:00:00","Activity":"Synthetic"}]', "LastAction" = 'U', "LastActionAtUtc" = "LastActionAtUtc" + interval '1 second'""",
                """UPDATE "Classrooms" SET "WeeklyNotesJson" = '[{"WeekStart":"2026-11-03","Text":"Synthetic"}]', "LastAction" = 'U', "LastActionAtUtc" = "LastActionAtUtc" + interval '1 second'"""
            })
            {
                await using var command = new NpgsqlCommand(statement, sql);
                Assert.Equal("23514", (await Assert.ThrowsAsync<PostgresException>(() => command.ExecuteNonQueryAsync())).SqlState);
            }
        }, migrateTarget: null);
    }

    private static ClassroomUpdate Update(ClassroomDetail detail) => new("Updated Synthetic Class", "Synthetic Room", 1,
        [new(new(7, 0), new(8, 0), "Synthetic breakfast"), new(new(8, 0), new(9, 0), "Synthetic lesson")],
        [new(new(2026, 11, 2), "Synthetic weekly note")], detail.LastActionAtUtc);

    private static WebApplicationFactory<Program> Factory(string? connection = null, int offsetDays = 0) =>
        new CustomWebApplicationFactory { EnableDevelopmentSeed = false }.WithWebHostBuilder(builder =>
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new FixedClock(offsetDays));
                if (connection is null) return;
                services.RemoveAll<DbContextOptions<ApplicationDbContext>>();
                services.RemoveAll<ApplicationDbContext>();
                services.RemoveAll<IDbContextOptionsConfiguration<ApplicationDbContext>>();
                services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connection));
            }));

    private sealed record Fixture(Guid SchoolId, Guid YearId, Guid ClassId, Guid SecondClassId, string Admin, string Teacher, string Parent);
    private static async Task<Fixture> Seed(WebApplicationFactory<Program> factory)
    {
        using var scope = factory.Services.CreateScope();
        using var audit = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("class-test-fixture");
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var school = new School { Id = Guid.NewGuid(), Name = "Synthetic School", Slug = $"class-{Guid.NewGuid():N}" };
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
        var first = new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "Synthetic Class" };
        var second = new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "Second Synthetic Class" };
        db.SchoolYears.Add(year); db.Classrooms.AddRange(first, second);
        var profile = new Teacher { Id = Guid.NewGuid(), SchoolId = school.Id, UserId = accounts[1].Id, Name = "Synthetic Teacher",
            Birthday = Today.AddYears(-30), Gender = "Nữ", Phone = "0900000000", Email = accounts[1].Email!,
            Degree = "Synthetic degree", Graduated = 2017, University = "Synthetic University", StartDate = Today.AddYears(-1),
            Contract = "Synthetic contract", Work = "Synthetic work" };
        db.Teachers.Add(profile);
        var students = Enumerable.Range(0, 3).Select(index => new Student { Id = Guid.NewGuid(), SchoolId = school.Id,
            FirstName = "Child", LastName = "Synthetic", DateOfBirth = Today.AddYears(-4) }).ToArray();
        db.Students.AddRange(students);
        await db.SaveChangesAsync();
        db.TeacherAssignments.Add(new TeacherAssignment { Id = Guid.NewGuid(), SchoolId = school.Id, TeacherId = profile.Id,
            ClassroomId = first.Id, Role = "Giáo viên chủ nhiệm", StartedAtUtc = DateTimeOffset.UtcNow });
        for (var index = 0; index < 2; index++)
            db.StudentInClasses.Add(new StudentInClass { Id = Guid.NewGuid(), SchoolId = school.Id, StudentId = students[index].Id, ClassroomId = first.Id,
                StartDate = Today.AddDays(-3),
                EndDate = Today.AddDays(31), AgeGroup = "Mầm · 3–4 tuổi", StudyMode = "Bán trú" });
        await db.SaveChangesAsync();
        (await db.StudentInClasses.SingleAsync(row => row.StudentId == students[1].Id)).EndDate = Today;
        await db.SaveChangesAsync();
        return new(school.Id, year.Id, first.Id, second.Id, accounts[0].Email!, accounts[1].Email!, accounts[2].Email!);
    }

    private static async Task<HttpClient> Login(WebApplicationFactory<Program> factory, string email)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), HandleCookies = true });
        var response = await Send(client, "/api/auth/login", new { email, password = Password }, HttpMethod.Post);
        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        client.DefaultRequestHeaders.Authorization = new("Bearer", payload.GetProperty("accessToken").GetString());
        return client;
    }
    private static Task<HttpResponseMessage> Put(HttpClient client, Guid id, object payload) => Send(client, $"/api/classes/{id}", payload, HttpMethod.Put);
    private static async Task<HttpResponseMessage> Send(HttpClient client, string path, object payload, HttpMethod method)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/api/auth/csrf");
        using var request = new HttpRequestMessage(method, path) { Content = JsonContent.Create(payload) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("requestToken").GetString());
        return await client.SendAsync(request);
    }
}
