using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;

namespace PreschoolConnect.Tests;

public class SchoolYearPostgresTests
{
    [PostgresFact]
    public async Task MigrationProtectsNamesDatesUsedYearsAndSchoolBoundaries()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            var school = Guid.NewGuid();
            var other = Guid.NewGuid();
            var year = Guid.NewGuid();
            await ExecuteAsync(connection, $"""
                INSERT INTO "Schools" ("Id", "Name", "Slug", "IsActive", "CreatedUtc") VALUES
                  ('{school}', 'Synthetic school', 'synthetic-1', true, now()),
                  ('{other}', 'Other synthetic school', 'synthetic-2', true, now());
                INSERT INTO "SchoolYears" ("Id", "SchoolId", "Name", "StartDate", "EndDate")
                  VALUES ('{year}', '{school}', 'Test Year', '2026-09-01', '2027-06-30');
                """);
            await AssertConstraintAsync(connection, $"""
                INSERT INTO "SchoolYears" ("Id", "SchoolId", "Name", "StartDate", "EndDate")
                  VALUES ('{Guid.NewGuid()}', '{school}', ' test year ', '2027-07-01', '2028-06-30')
                """, "UX_SchoolYears_NormalizedName");
            await AssertConstraintAsync(connection, $"""
                INSERT INTO "SchoolYears" ("Id", "SchoolId", "Name", "StartDate", "EndDate")
                  VALUES ('{Guid.NewGuid()}', '{school}', 'Next Year', '2027-06-30', '2028-06-30')
                """, "CK_SchoolYears_NoOverlap");
            await ExecuteAsync(connection, $"UPDATE \"SchoolYears\" SET \"Name\" = 'NĂM HỌC MỚI' WHERE \"Id\" = '{year}'");
            await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options))
            {
                Assert.True(await db.SchoolYears.IgnoreQueryFilters(["SoftDelete"]).AnyAsync(item => item.SchoolId == school &&
                    EF.Functions.Collate(item.Name.Trim(), "pg_c_utf8").ToLower() == "năm học mới"));
            }
            await AssertConstraintAsync(connection, $"""
                INSERT INTO "SchoolYears" ("Id", "SchoolId", "Name", "StartDate", "EndDate")
                  VALUES ('{Guid.NewGuid()}', '{school}', ' năm học mới ', '2027-07-01', '2028-06-30')
                """, "UX_SchoolYears_NormalizedName");
            await ExecuteAsync(connection, $"""
                INSERT INTO "SchoolYears" ("Id", "SchoolId", "Name", "StartDate", "EndDate")
                  VALUES ('{Guid.NewGuid()}', '{other}', 'NĂM HỌC MỚI', '2026-09-01', '2027-06-30');
                INSERT INTO "Classrooms" ("Id", "SchoolId", "SchoolYearId", "Name")
                  VALUES ('{Guid.NewGuid()}', '{school}', '{year}', 'Synthetic class');
                UPDATE "SchoolYears" SET "Name" = 'Renamed' WHERE "Id" = '{year}';
                """);
            await AssertConstraintAsync(connection, $"UPDATE \"SchoolYears\" SET \"StartDate\" = '2026-09-02' WHERE \"Id\" = '{year}'", "CK_SchoolYears_DatesInUse");
            await AssertConstraintAsync(connection, $"DELETE FROM \"SchoolYears\" WHERE \"Id\" = '{year}'", "CK_SchoolYears_DatesInUse");
        });
    }

    [PostgresFact]
    public async Task ConcurrentOverlapAndClassCreationCannotBypassGuards()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await using var first = new NpgsqlConnection(connectionString);
            await using var second = new NpgsqlConnection(connectionString);
            await first.OpenAsync();
            await second.OpenAsync();
            var school = Guid.NewGuid();
            var year = Guid.NewGuid();
            await ExecuteAsync(first, $"INSERT INTO \"Schools\" (\"Id\", \"Name\", \"Slug\", \"IsActive\", \"CreatedUtc\") VALUES ('{school}', 'Synthetic school', 'concurrent', true, now())");
            await using (var transaction = await first.BeginTransactionAsync())
            {
                await ExecuteAsync(first, $"""
                    INSERT INTO "SchoolYears" ("Id", "SchoolId", "Name", "StartDate", "EndDate")
                    VALUES ('{year}', '{school}', 'First', '2026-09-01', '2027-06-30')
                    """);
                var blocked = ExecuteAsync(second, $"""
                    INSERT INTO "SchoolYears" ("Id", "SchoolId", "Name", "StartDate", "EndDate")
                    VALUES ('{Guid.NewGuid()}', '{school}', 'Second', '2027-01-01', '2027-12-31')
                    """);
                await WaitForAdvisoryLockAsync(first, second.ProcessID);
                await transaction.CommitAsync();
                var exception = await Assert.ThrowsAsync<PostgresException>(async () => await blocked);
                Assert.Equal("CK_SchoolYears_NoOverlap", exception.ConstraintName);
            }
            await using (var transaction = await first.BeginTransactionAsync())
            {
                await ExecuteAsync(first, $"""
                    INSERT INTO "Classrooms" ("Id", "SchoolId", "SchoolYearId", "Name")
                    VALUES ('{Guid.NewGuid()}', '{school}', '{year}', 'Concurrent class')
                    """);
                var blocked = ExecuteAsync(second, $"UPDATE \"SchoolYears\" SET \"EndDate\" = '2027-07-01' WHERE \"Id\" = '{year}'");
                await WaitForAdvisoryLockAsync(first, second.ProcessID);
                await transaction.CommitAsync();
                var exception = await Assert.ThrowsAsync<PostgresException>(async () => await blocked);
                Assert.Equal("CK_SchoolYears_DatesInUse", exception.ConstraintName);
            }
        });
    }

    [PostgresFact]
    public async Task MigrationRefusesConflictingExistingDataWithoutChangingIt()
    {
        await WithDatabaseAsync(async connectionString =>
        {
            await using var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connectionString).Options);
            var migrator = db.GetService<IMigrator>();
            await migrator.MigrateAsync("20261030092720_SchoolYearsAndClassrooms");
            await using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync();
            var school = Guid.NewGuid();
            await ExecuteAsync(connection, $"""
                INSERT INTO "Schools" ("Id", "Name", "Slug", "IsActive", "CreatedUtc") VALUES ('{school}', 'Synthetic school', 'legacy', true, now());
                INSERT INTO "SchoolYears" ("Id", "SchoolId", "Name", "StartDate", "EndDate") VALUES
                  ('{Guid.NewGuid()}', '{school}', 'Legacy', '2026-09-01', '2027-06-30'),
                  ('{Guid.NewGuid()}', '{school}', ' legacy ', '2026-10-01', '2027-05-31');
                """);
            var exception = await Assert.ThrowsAsync<PostgresException>(() => migrator.MigrateAsync());
            Assert.Contains("Correct existing records", exception.MessageText);
            await using var count = new NpgsqlCommand("SELECT count(*) FROM \"SchoolYears\"", connection);
            Assert.Equal(2L, await count.ExecuteScalarAsync());
        });
    }

    internal static async Task WithDatabaseAsync(Func<string, Task> test, string? migrateTarget = "20261103030000_SchoolYearIntegrity")
    {
        var settings = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable("PRESCHOOL_TEST_POSTGRES"));
        if (settings.Host != "127.0.0.1" || settings.Database != "postgres" || settings.Port < 1024)
            throw new InvalidOperationException("Use a loopback-only isolated PostgreSQL server and the postgres maintenance database.");
        var database = $"preschool_year_test_{Guid.NewGuid():N}";
        await using var admin = new NpgsqlConnection(settings.ConnectionString);
        await admin.OpenAsync();
        await ExecuteAsync(admin, $"CREATE DATABASE \"{database}\"");
        settings.Database = database;
        try
        {
            await using (var db = new ApplicationDbContext(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(settings.ConnectionString).Options))
                await db.GetService<IMigrator>().MigrateAsync(migrateTarget);
            await test(settings.ConnectionString);
        }
        finally
        {
            NpgsqlConnection.ClearAllPools();
            await ExecuteAsync(admin, $"DROP DATABASE \"{database}\" WITH (FORCE)");
        }
    }

    internal static async Task WaitForAdvisoryLockAsync(NpgsqlConnection connection, int pid)
    {
        for (var attempt = 0; attempt < 100; attempt++)
        {
            await using var command = new NpgsqlCommand("SELECT EXISTS (SELECT 1 FROM pg_locks WHERE pid = @pid AND locktype = 'advisory' AND NOT granted)", connection);
            command.Parameters.AddWithValue("pid", pid);
            if (await command.ExecuteScalarAsync() is true) return;
            await Task.Delay(20);
        }
        throw new TimeoutException("Concurrent synthetic writer did not reach the school-year advisory lock.");
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task AssertConstraintAsync(NpgsqlConnection connection, string sql, string constraint)
    {
        var exception = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql));
        Assert.Equal(constraint, exception.ConstraintName);
    }

    public sealed class PostgresFactAttribute : FactAttribute
    {
        public PostgresFactAttribute()
        {
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PRESCHOOL_TEST_POSTGRES")))
                Skip = "Set PRESCHOOL_TEST_POSTGRES to an isolated loopback test server to run PostgreSQL integrity tests.";
        }
    }
}
