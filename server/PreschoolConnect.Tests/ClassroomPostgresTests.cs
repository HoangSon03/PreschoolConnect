using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;
using static PreschoolConnect.Tests.SchoolYearPostgresTests;

namespace PreschoolConnect.Tests;

public class ClassroomPostgresTests
{
    private static ApplicationDbContext Context(string connection) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options, new AuditingTests.ActorProvider());

    [Fact]
    public void MigrationIsDiscoverableWithoutModelDriftAndGeneratesGuardedSql()
    {
        using var db = Context("Host=127.0.0.1;Port=1;Database=unavailable;Username=test");
        Assert.False(db.Database.HasPendingModelChanges());
        var script = db.GetService<IMigrator>().GenerateScript("20261103032915_StandardizedAuditing");
        Assert.Contains("UX_Classrooms_NormalizedName", script);
        Assert.Contains("CK_Classrooms_ImmutableSchoolYear", script);
        Assert.Contains("Correct existing records", script);
        Assert.Contains("pg_c_utf8", script);
    }

    [PostgresFact]
    public async Task NamesAreUniqueUnderConcurrentWritesAndYearHistoryIsImmutable()
    {
        await WithDatabaseAsync(async connection =>
        {
            await using var db = Context(connection);
            var school = new School { Id = Guid.NewGuid(), Name = "Synthetic class school", Slug = "class-integrity" };
            var year = Year(school.Id, "First", 2026);
            var next = Year(school.Id, "Next", 2027);
            db.AddRange(school, year, next);
            await db.SaveChangesAsync();
            await using var first = new NpgsqlConnection(connection);
            await using var second = new NpgsqlConnection(connection);
            await first.OpenAsync();
            await second.OpenAsync();
            var id = Guid.NewGuid();
            await using (var transaction = await first.BeginTransactionAsync())
            {
                await ExecuteAsync(first, InsertClass(id, school.Id, year.Id, "LỚP MẦM"));
                var blocked = ExecuteAsync(second, InsertClass(Guid.NewGuid(), school.Id, year.Id, " lớp mầm "));
                await WaitForAdvisoryLockAsync(first, second.ProcessID);
                await transaction.CommitAsync();
                var conflict = await Assert.ThrowsAsync<PostgresException>(() => blocked);
                Assert.Equal("UX_Classrooms_NormalizedName", conflict.ConstraintName);
            }
            Assert.True(await db.Classrooms.AnyAsync(item =>
                EF.Functions.Collate(item.Name.Trim(), "pg_c_utf8").ToLower() == "lớp mầm"));
            var immutable = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(first, $"""
                UPDATE "Classrooms" SET "SchoolYearId" = '{next.Id}', "LastAction" = 'U',
                  "LastActionAtUtc" = "LastActionAtUtc" + interval '1 second' WHERE "Id" = '{id}'
                """));
            Assert.Equal("CK_Classrooms_ImmutableSchoolYear", immutable.ConstraintName);
            await ExecuteAsync(first, InsertClass(Guid.NewGuid(), school.Id, next.Id, "lớp mầm"));
            var retained = await db.Classrooms.SingleAsync(item => item.Id == id);
            db.Classrooms.Remove(retained);
            await db.SaveChangesAsync();
            await ExecuteAsync(first, InsertClass(Guid.NewGuid(), school.Id, year.Id, "lớp mầm"));
            Assert.Equal(year.Id, (await db.Classrooms.IgnoreQueryFilters().SingleAsync(item => item.Id == id)).SchoolYearId);
            Assert.Equal(3, await db.Classrooms.IgnoreQueryFilters().CountAsync());
        }, migrateTarget: null);
    }

    [PostgresFact]
    public async Task MigrationRejectsLegacyCaseConflictsWithoutRewritingHistory()
    {
        await WithDatabaseAsync(async connection =>
        {
            await using var db = Context(connection);
            var school = new School { Id = Guid.NewGuid(), Name = "Synthetic legacy school", Slug = "legacy-class" };
            var year = Year(school.Id, "Legacy", 2026);
            db.AddRange(school, year);
            db.Classrooms.AddRange(
                new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "LỚP MẦM" },
                new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "lớp mầm" });
            await db.SaveChangesAsync();
            var before = await db.AuditLogs.CountAsync();
            var exception = await Assert.ThrowsAsync<PostgresException>(() => db.Database.MigrateAsync());
            Assert.Contains("Classroom names conflict. Correct existing records", exception.MessageText);
            Assert.Equal(new[] { "LỚP MẦM", "lớp mầm" },
                (await db.Classrooms.Select(item => item.Name).ToListAsync()).OrderBy(name => name, StringComparer.Ordinal));
            Assert.Equal(before, await db.AuditLogs.CountAsync());
            Assert.Contains("20261104080000_ClassroomIntegrity", await db.Database.GetPendingMigrationsAsync());
        }, migrateTarget: "20261103032915_StandardizedAuditing");
    }

    private static SchoolYear Year(Guid school, string name, int start) => new()
    {
        Id = Guid.NewGuid(), SchoolId = school, Name = name,
        StartDate = new(start, 9, 1), EndDate = new(start + 1, 6, 30)
    };

    private static string InsertClass(Guid id, Guid school, Guid year, string name) => $"""
        INSERT INTO "Classrooms" ("Id","SchoolId","SchoolYearId","Name","CreatedAtUtc","LastAction","LastActionAtUtc")
        VALUES ('{id}','{school}','{year}','{name}',now(),'I',now())
        """;

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }
}
