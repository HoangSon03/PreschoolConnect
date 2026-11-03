using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;
using static PreschoolConnect.Tests.SchoolYearPostgresTests;

namespace PreschoolConnect.Tests;

public class AuditPostgresTests
{
    private static ApplicationDbContext Context(string connection, AuditingTests.ActorProvider actor) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseNpgsql(connection).Options, actor);

    [PostgresFact]
    public async Task MigrationBackfillsWithoutInventingHistoricalActorsAndMatchesModel()
    {
        await WithDatabaseAsync(async connection =>
        {
            var schoolId = Guid.NewGuid();
            var yearId = Guid.NewGuid();
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            await ExecuteAsync(sql, $"""
                INSERT INTO "Schools" ("Id","Name","Slug","IsActive","CreatedUtc")
                  VALUES ('{schoolId}','Synthetic legacy','legacy-audit',true,'2025-01-02T03:04:05Z');
                INSERT INTO "SchoolYears" ("Id","SchoolId","Name","StartDate","EndDate")
                  VALUES ('{yearId}','{schoolId}','Legacy year','2026-09-01','2027-06-30');
                """);
            await using var db = Context(connection, new());
            Assert.Equal(SchoolContextFailure.SchemaNotReady, (await new SchoolContextResolver(db).ResolveAsync(Guid.NewGuid(), schoolId, default)).Failure);
            await db.Database.MigrateAsync();
            Assert.False(db.Database.HasPendingModelChanges());
            var school = await db.Schools.SingleAsync();
            var year = await db.SchoolYears.SingleAsync();
            Assert.Equal(new DateTimeOffset(2025, 1, 2, 3, 4, 5, TimeSpan.Zero), school.CreatedAtUtc);
            Assert.Equal(school.CreatedUtc, school.CreatedAtUtc);
            Assert.Null(school.CreatedByUserId);
            Assert.Null(year.CreatedByUserId);
            Assert.Null(year.LastActionByUserId);
            Assert.Equal("I", year.LastAction);
            Assert.Equal(year.CreatedAtUtc, year.LastActionAtUtc);
            Assert.NotEqual(SchoolContextFailure.SchemaNotReady, (await new SchoolContextResolver(db).ResolveAsync(Guid.NewGuid(), schoolId, default)).Failure);
            Assert.Empty(await db.AuditLogs.ToListAsync());
            await Assert.ThrowsAsync<PostgresException>(() => db.GetService<IMigrator>().MigrateAsync("20261103030000_SchoolYearIntegrity"));
            Assert.Equal(1, await db.SchoolYears.CountAsync());
        });
    }

    [PostgresFact]
    public async Task PostgreSqlProtectsAuditStatePartialUniquenessAndActorReferences()
    {
        await WithDatabaseAsync(async connection =>
        {
            var actor = new AuditingTests.ActorProvider();
            await using var db = Context(connection, actor);
            var school = new School { Id = Guid.NewGuid(), Slug = "constraint-audit", Name = "Synthetic school" };
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "synthetic-auditor", IsActive = true };
            db.AddRange(school, user);
            db.SchoolMemberships.Add(new SchoolMembership { Id = Guid.NewGuid(), SchoolId = school.Id, UserId = user.Id });
            await db.SaveChangesAsync();
            actor.Actor = new(user.Id, null);
            var year = Year(school.Id, "NĂM HỌC");
            db.SchoolYears.Add(year);
            await db.SaveChangesAsync();
            var created = year.CreatedAtUtc;
            year.Name = "NĂM HỌC MỚI";
            db.SaveChanges();
            Assert.Equal(created, year.CreatedAtUtc);
            db.SchoolYears.Remove(year);
            await db.SaveChangesAsync();
            var replacement = Year(school.Id, " năm học mới ");
            db.SchoolYears.Add(replacement);
            await db.SaveChangesAsync();
            Assert.Equal(2, await db.SchoolYears.IgnoreQueryFilters().CountAsync());
            Assert.Equal(1, await db.SchoolYears.CountAsync());
            var classroom = new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = replacement.Id, Name = "Synthetic class" };
            db.Classrooms.Add(classroom);
            await db.SaveChangesAsync();
            db.Classrooms.Remove(classroom);
            await db.SaveChangesAsync();
            db.Classrooms.Add(new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = replacement.Id, Name = "Synthetic class" });
            await db.SaveChangesAsync();
            await using var sql = new NpgsqlConnection(connection);
            await sql.OpenAsync();
            await ConstraintAsync(sql, $"DELETE FROM \"SchoolYears\" WHERE \"Id\" = '{year.Id}'", "CK_Business_NoPhysicalDelete");
            await ConstraintAsync(sql, $"UPDATE \"SchoolYears\" SET \"Name\" = 'Invalid' WHERE \"Id\" = '{year.Id}'", "CK_Business_DeletedImmutable");
            await ConstraintAsync(sql, $"UPDATE \"SchoolYears\" SET \"Name\" = 'Bypass' WHERE \"Id\" = '{replacement.Id}'", "CK_Business_UpdateAudit");
            await ConstraintAsync(sql, $"UPDATE \"SchoolYears\" SET \"LastAction\" = 'X' WHERE \"Id\" = '{replacement.Id}'", "CK_Business_UpdateAudit");
            await ConstraintAsync(sql, $"DELETE FROM \"AuditLogs\" WHERE \"EntityId\" = '{year.Id}'", "CK_AuditLogs_AppendOnly");
            var bulkDelete = await Assert.ThrowsAsync<PostgresException>(() => db.SchoolYears.Where(item => item.Id == replacement.Id).ExecuteDeleteAsync());
            Assert.Equal("CK_Business_NoPhysicalDelete", bulkDelete.ConstraintName);
            var bulkUpdate = await Assert.ThrowsAsync<PostgresException>(() => db.SchoolYears.Where(item => item.Id == replacement.Id)
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.Name, "Unsupported bulk update")));
            Assert.Equal("CK_Business_UpdateAudit", bulkUpdate.ConstraintName);
            await ExecuteAsync(sql, "ALTER TABLE \"SchoolYears\" DISABLE TRIGGER USER");
            try
            {
                await ConstraintAsync(sql, $"""
                    INSERT INTO "SchoolYears" ("Id","SchoolId","Name","StartDate","EndDate","CreatedAtUtc","LastActionAtUtc","LastAction")
                    VALUES ('{Guid.NewGuid()}','{school.Id}','Invalid audit CHECK','2030-09-01','2031-06-30',now(),now(),'X')
                    """, "CK_SchoolYears_LastAction");
            }
            finally { await ExecuteAsync(sql, "ALTER TABLE \"SchoolYears\" ENABLE TRIGGER USER"); }
            var deletion = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql, $"DELETE FROM \"AspNetUsers\" WHERE \"Id\" = '{user.Id}'"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, deletion.SqlState);
            var historicalActor = new ApplicationUser { Id = Guid.NewGuid(), UserName = "synthetic-historical-actor" };
            db.Users.Add(historicalActor);
            await db.SaveChangesAsync();
            await ExecuteAsync(sql, $"""
                INSERT INTO "Schools" ("Id","Name","Slug","IsActive","CreatedUtc","CreatedAtUtc","CreatedByUserId","LastAction","LastActionByUserId","LastActionAtUtc")
                VALUES ('{Guid.NewGuid()}','Synthetic audit FK','actor-fk',true,now(),now(),'{historicalActor.Id}','I','{historicalActor.Id}',now())
                """);
            var auditFk = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(sql, $"DELETE FROM \"AspNetUsers\" WHERE \"Id\" = '{historicalActor.Id}'"));
            Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, auditFk.SqlState);
            Assert.StartsWith("FK_Schools_AspNetUsers_", auditFk.ConstraintName);
            var duplicate = Year(school.Id, "NĂM HỌC MỚI");
            duplicate.StartDate = new(2028, 9, 1);
            duplicate.EndDate = new(2029, 6, 30);
            db.Add(duplicate);
            var unique = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal("UX_SchoolYears_NormalizedName", Assert.IsType<PostgresException>(unique.InnerException).ConstraintName);
            duplicate.Name = "Retry after synthetic conflict";
            await db.SaveChangesAsync();
            Assert.Equal(1, await db.AuditLogs.CountAsync(log => log.EntityId == duplicate.Id));
            db.ChangeTracker.Clear();
            Assert.Equal(new[] { "I", "U", "D" }, await db.AuditLogs.Where(log => log.EntityId == year.Id)
                .OrderBy(log => log.OccurredAtUtc).Select(log => log.Action).ToArrayAsync());
        }, migrateTarget: null);
    }

    [PostgresFact]
    public async Task ConcurrentAuditedWritesPreserveOverlapAndRetainedClassGuards()
    {
        await WithDatabaseAsync(async connection =>
        {
            var actor = new AuditingTests.ActorProvider();
            await using var first = Context(connection, actor);
            await using var second = Context(connection, actor);
            var school = new School { Id = Guid.NewGuid(), Slug = "audited-writers", Name = "Synthetic concurrent writers" };
            first.Add(school);
            await first.SaveChangesAsync();
            await first.Database.OpenConnectionAsync();
            await second.Database.OpenConnectionAsync();
            var firstSql = (NpgsqlConnection)first.Database.GetDbConnection();
            var secondSql = (NpgsqlConnection)second.Database.GetDbConnection();
            var year = Year(school.Id, "First");
            await using (var transaction = await first.Database.BeginTransactionAsync())
            {
                first.Add(year);
                await first.SaveChangesAsync();
                second.Add(Year(school.Id, "Overlapping"));
                var waiting = second.SaveChangesAsync();
                await WaitForAdvisoryLockAsync(firstSql, secondSql.ProcessID);
                await transaction.CommitAsync();
                var error = await Assert.ThrowsAsync<DbUpdateException>(async () => await waiting);
                Assert.Equal("CK_SchoolYears_NoOverlap", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
            }
            second.ChangeTracker.Clear();
            var candidate = await second.SchoolYears.SingleAsync(item => item.Id == year.Id);
            await using (var transaction = await first.Database.BeginTransactionAsync())
            {
                first.Add(new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "Retained concurrent class" });
                await first.SaveChangesAsync();
                second.Remove(candidate);
                var waiting = second.SaveChangesAsync();
                await WaitForAdvisoryLockAsync(firstSql, secondSql.ProcessID);
                await transaction.CommitAsync();
                var error = await Assert.ThrowsAsync<DbUpdateException>(async () => await waiting);
                Assert.Equal("CK_SchoolYears_DatesInUse", Assert.IsType<PostgresException>(error.InnerException).ConstraintName);
            }
            second.ChangeTracker.Clear();
            Assert.Equal("I", (await second.SchoolYears.SingleAsync()).LastAction);
            Assert.Single(await second.Classrooms.ToListAsync());
            Assert.DoesNotContain(await second.AuditLogs.Where(log => log.EntityId == year.Id).ToListAsync(), log => log.Action == "D");
        }, migrateTarget: null);
    }

    [PostgresFact]
    public async Task StaleUpdatesCannotResurrectDeletedRowsAndMappingsRemainReserved()
    {
        await WithDatabaseAsync(async connection =>
        {
            var actor = new AuditingTests.ActorProvider();
            await using var db = Context(connection, actor);
            var school = new School { Id = Guid.NewGuid(), Name = "Synthetic concurrency", Slug = "audit-concurrency" };
            var student = new Student { Id = Guid.NewGuid(), SchoolId = school.Id, FirstName = "Synthetic", DateOfBirth = new(2020, 1, 1) };
            var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = "synthetic-parent" };
            var account = new StudentAccount { Id = Guid.NewGuid(), SchoolId = school.Id, StudentId = student.Id, UserId = user.Id };
            db.AddRange(school, student, user, account);
            await db.SaveChangesAsync();
            db.Remove(account);
            await db.SaveChangesAsync();
            db.Remove(student);
            await Assert.ThrowsAsync<AuditPolicyException>(() => db.SaveChangesAsync());
            db.Entry(student).State = EntityState.Unchanged;
            db.Add(new StudentAccount { Id = Guid.NewGuid(), SchoolId = school.Id, StudentId = student.Id, UserId = user.Id });
            var unique = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            Assert.Equal(PostgresErrorCodes.UniqueViolation, Assert.IsType<PostgresException>(unique.InnerException).SqlState);
            db.ChangeTracker.Clear();
            var year = Year(school.Id, "Concurrency");
            db.Add(year);
            await db.SaveChangesAsync();
            await using var stale = Context(connection, actor);
            var staleYear = await stale.SchoolYears.SingleAsync(item => item.Id == year.Id);
            db.Remove(year);
            await db.SaveChangesAsync();
            staleYear.Name = "Stale edit";
            await Assert.ThrowsAsync<AuditPolicyException>(() => stale.SaveChangesAsync());
            Assert.Empty(await db.SchoolYears.ToListAsync());
            Assert.Equal("D", (await db.SchoolYears.IgnoreQueryFilters().SingleAsync()).LastAction);
        }, migrateTarget: null);
    }

    private static SchoolYear Year(Guid schoolId, string name) => new()
    {
        Id = Guid.NewGuid(), SchoolId = schoolId, Name = name, StartDate = new(2026, 9, 1), EndDate = new(2027, 6, 30)
    };

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql)
    {
        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task ConstraintAsync(NpgsqlConnection connection, string sql, string constraint)
    {
        var error = await Assert.ThrowsAsync<PostgresException>(() => ExecuteAsync(connection, sql));
        Assert.Equal(constraint, error.ConstraintName);
    }
}
