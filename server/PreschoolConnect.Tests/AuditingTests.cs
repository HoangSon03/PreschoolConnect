using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;
using Xunit;

namespace PreschoolConnect.Tests;

public class AuditingTests
{
    internal sealed class ActorProvider : IAuditActorProvider
    {
        public AuditActor Actor { get; set; } = new(null, "synthetic-test-job");
        public AuditActor GetActor() => Actor;
    }

    private static ApplicationDbContext CreateContext(ActorProvider? actor = null) =>
        new(new DbContextOptionsBuilder<ApplicationDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options, actor);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task InsertUpdateDeletePreservesCreationAndRecordsHistory(bool async)
    {
        var actor = new ActorProvider();
        await using var db = CreateContext(actor);
        var school = new School { Id = Guid.NewGuid(), Slug = "synthetic", Name = "Synthetic school" };
        var user = new ApplicationUser { Id = Guid.NewGuid(), IsActive = true };
        db.Users.Add(user);
        db.Schools.Add(school);
        db.SchoolMemberships.Add(new SchoolMembership { Id = Guid.NewGuid(), UserId = user.Id, SchoolId = school.Id });
        await SaveAsync(db, async);
        actor.Actor = new(user.Id, null);
        var year = new SchoolYear { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "Synthetic year",
            StartDate = new(2026, 9, 1), EndDate = new(2027, 6, 30) };
        db.SchoolYears.Add(year);
        await SaveAsync(db, async);
        Assert.Equal("I", year.LastAction);
        Assert.Equal(user.Id, year.CreatedByUserId);
        Assert.Equal(user.Id, year.LastActionByUserId);
        Assert.Equal(TimeSpan.Zero, year.CreatedAtUtc.Offset);
        Assert.Equal(year.CreatedAtUtc, year.LastActionAtUtc);
        var created = year.CreatedAtUtc;
        year.Name = "Synthetic renamed";
        await SaveAsync(db, async);
        Assert.Equal("U", year.LastAction);
        Assert.Equal(created, year.CreatedAtUtc);
        Assert.Equal(user.Id, year.CreatedByUserId);
        Assert.True(year.LastActionAtUtc > created);
        db.SchoolYears.Remove(year);
        await SaveAsync(db, async);
        Assert.Equal("D", year.LastAction);
        Assert.Equal(created, year.CreatedAtUtc);
        Assert.Equal(user.Id, year.CreatedByUserId);
        db.ChangeTracker.Clear();
        Assert.Empty(await db.SchoolYears.ToListAsync());
        var deleted = await db.SchoolYears.IgnoreQueryFilters().SingleAsync();
        Assert.Equal("D", deleted.LastAction);
        var history = await db.AuditLogs.Where(log => log.EntityId == year.Id).OrderBy(log => log.OccurredAtUtc).ToListAsync();
        Assert.Equal(new[] { "I", "U", "D" }, history.Select(log => log.Action));
        Assert.Equal("[\"Name\"]", history[1].ChangedFields);
        Assert.DoesNotContain("renamed", history[1].ChangedFields);
        deleted.Name = "Invalid resurrection";
        await Assert.ThrowsAsync<AuditPolicyException>(() => SaveAsync(db, async));
        db.Entry(deleted).State = EntityState.Unchanged;
        db.SchoolYears.Remove(deleted);
        await Assert.ThrowsAsync<AuditPolicyException>(() => SaveAsync(db, async));
    }

    [Fact]
    public async Task MissingOrCrossTenantActorsFailAndSystemActorsRemainExplicit()
    {
        await using var missing = CreateContext();
        missing.Schools.Add(new School { Id = Guid.NewGuid(), Slug = "missing" });
        await Assert.ThrowsAsync<InvalidOperationException>(() => missing.SaveChangesAsync());
        var actor = new ActorProvider();
        await using var db = CreateContext(actor);
        var school = new School { Id = Guid.NewGuid(), Slug = "system" };
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        Assert.Null(school.CreatedByUserId);
        Assert.Equal("synthetic-test-job", (await db.AuditLogs.SingleAsync()).SystemOperation);
        var user = new ApplicationUser { Id = Guid.NewGuid(), IsActive = true };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        actor.Actor = new(user.Id, null);
        school.Name = "Wrong tenant";
        await Assert.ThrowsAsync<AuditPolicyException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task AuditTamperingDetachedUpdatesAndAuditLogMutationAreRejected()
    {
        var actor = new ActorProvider();
        await using var db = CreateContext(actor);
        var school = new School { Id = Guid.NewGuid(), Slug = "tamper" };
        db.Schools.Add(school);
        await db.SaveChangesAsync();
        var created = school.CreatedAtUtc;
        db.Entry(school).Property(nameof(School.CreatedAtUtc)).CurrentValue = created.AddDays(1);
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.Update(new School { Id = school.Id, Slug = "tamper", Name = "Detached overwrite" });
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        db.ChangeTracker.Clear();
        db.AuditLogs.Remove(await db.AuditLogs.SingleAsync());
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task HistoricalClassesAndMappingsBlockParentDeletion()
    {
        var actor = new ActorProvider();
        await using var db = CreateContext(actor);
        var school = new School { Id = Guid.NewGuid(), Slug = "history" };
        var year = new SchoolYear { Id = Guid.NewGuid(), SchoolId = school.Id, Name = "History", StartDate = new(2026, 9, 1), EndDate = new(2027, 6, 30) };
        var classroom = new Classroom { Id = Guid.NewGuid(), SchoolId = school.Id, SchoolYearId = year.Id, Name = "Synthetic class" };
        db.AddRange(school, year, classroom);
        await db.SaveChangesAsync();
        db.Classrooms.Remove(classroom);
        await db.SaveChangesAsync();
        db.SchoolYears.Remove(year);
        await Assert.ThrowsAsync<AuditPolicyException>(() => db.SaveChangesAsync());
        db.Entry(year).State = EntityState.Unchanged;
        year.EndDate = year.EndDate.AddDays(1);
        await Assert.ThrowsAsync<AuditPolicyException>(() => db.SaveChangesAsync());
        Assert.Equal(year.Id, (await db.Classrooms.IgnoreQueryFilters().SingleAsync()).SchoolYearId);
    }

    [Fact]
    public async Task DeletedMembershipCannotResolveTenantAndFalseAcceptSupportsExplicitAcceptance()
    {
        var actor = new ActorProvider();
        await using var db = CreateContext(actor);
        var school = new School { Id = Guid.NewGuid(), Slug = "membership" };
        var member = new SchoolMembership { Id = Guid.NewGuid(), SchoolId = school.Id, UserId = Guid.NewGuid() };
        member.Roles.Add(new SchoolMembershipRole { MembershipId = member.Id, Role = SchoolRoleNames.SchoolAdmin });
        db.AddRange(school, member);
        await db.SaveChangesAsync(false);
        db.ChangeTracker.AcceptAllChanges();
        Assert.NotNull((await new SchoolContextResolver(db).ResolveAsync(member.UserId, school.Id, default)).Context);
        db.SchoolMemberships.Remove(member);
        db.SaveChanges(false);
        db.ChangeTracker.AcceptAllChanges();
        db.ChangeTracker.Clear();
        Assert.Null((await new SchoolContextResolver(db).ResolveAsync(member.UserId, school.Id, default)).Context);
        Assert.Single(await db.SchoolMembershipRoles.ToListAsync());
    }

    private static async Task SaveAsync(ApplicationDbContext db, bool async)
    {
        if (async) await db.SaveChangesAsync(); else db.SaveChanges();
    }
}
