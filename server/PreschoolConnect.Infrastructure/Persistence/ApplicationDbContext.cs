using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Application.Auth;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PreschoolConnect.Infrastructure.Persistence;

public partial class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    private readonly IAuditActorProvider? auditActorProvider;
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, IAuditActorProvider? auditActorProvider = null) : base(options)
    {
        this.auditActorProvider = auditActorProvider;
        // Convert business deletes before EF processes tracked dependent relationships.
        ChangeTracker.CascadeDeleteTiming = Microsoft.EntityFrameworkCore.ChangeTracking.CascadeTiming.OnSaveChanges;
    }

    public DbSet<School> Schools => Set<School>();
    public DbSet<SchoolMembership> SchoolMemberships => Set<SchoolMembership>();
    public DbSet<SchoolMembershipRole> SchoolMembershipRoles => Set<SchoolMembershipRole>();
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentAccount> StudentAccounts => Set<StudentAccount>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Ignore<BaseAuditableEntity>();

        builder.Entity<School>(entity =>
        {
            ConfigureAudit(entity, "Schools");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        });

        builder.Entity<SchoolMembership>(entity =>
        {
            ConfigureAudit(entity, "SchoolMemberships");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SchoolId, x.UserId }).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.IsActive });
            entity.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SchoolMembershipRole>(entity =>
        {
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_SchoolMembershipRoles_Role",
                "\"Role\" IN ('SchoolAdmin', 'Teacher', 'Parent')"));
            entity.HasKey(x => new { x.MembershipId, x.Role });
            entity.Property(x => x.Role).HasMaxLength(32).IsRequired();
            entity.HasOne<SchoolMembership>()
                .WithMany(x => x.Roles)
                .HasForeignKey(x => x.MembershipId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<SchoolYear>(entity =>
        {
            ConfigureAudit(entity, "SchoolYears");
            entity.HasKey(x => x.Id);
            entity.HasAlternateKey(x => new { x.SchoolId, x.Id });
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.ToTable(table => table.HasCheckConstraint(
                "CK_SchoolYears_DateRange",
                "\"EndDate\" > \"StartDate\""));
            entity.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Classroom>(entity =>
        {
            ConfigureAudit(entity, "Classrooms");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SchoolYearId, x.Name }).IsUnique().HasFilter("\"LastAction\" <> 'D'");
            entity.Property(x => x.Name).HasMaxLength(100).IsRequired();
            entity.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<SchoolYear>()
                .WithMany()
                .HasForeignKey(x => new { x.SchoolId, x.SchoolYearId })
                .HasPrincipalKey(x => new { x.SchoolId, x.Id })
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Student>(entity =>
        {
            ConfigureAudit(entity, "Students");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SchoolId, x.LastName, x.FirstName }).IsUnique(false);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StudentAccount>(entity =>
        {
            ConfigureAudit(entity, "StudentAccounts");
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.StudentId).IsUnique();
            entity.HasIndex(x => new { x.SchoolId, x.StudentId }).IsUnique();
            entity.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ApplicationUser>(entity =>
        {
            entity.Property(x => x.FirstName).HasMaxLength(100);
            entity.Property(x => x.LastName).HasMaxLength(100);
            entity.HasIndex(x => x.SchoolId);
            entity.HasIndex(x => x.StudentId);
        });

        builder.Entity<ApplicationRole>(entity =>
        {
            entity.HasIndex(x => x.SchoolId);
        });
        builder.Entity<AuditLog>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.EntityType).HasMaxLength(64).IsRequired();
            entity.Property(x => x.Action).HasMaxLength(1).IsRequired();
            entity.Property(x => x.SystemOperation).HasMaxLength(100);
            entity.Property(x => x.ChangedFields).HasColumnType("jsonb").IsRequired();
            entity.Property(x => x.OccurredAtUtc).HasColumnType("timestamp with time zone");
            entity.HasIndex(x => new { x.SchoolId, x.OccurredAtUtc });
            entity.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.Restrict);
            entity.ToTable(table =>
            {
                table.HasCheckConstraint("CK_AuditLogs_Action", "\"Action\" IN ('I', 'U', 'D')");
                table.HasCheckConstraint("CK_AuditLogs_Actor", "(\"ActorUserId\" IS NOT NULL AND \"SystemOperation\" IS NULL) OR (\"ActorUserId\" IS NULL AND \"SystemOperation\" IS NOT NULL)");
            });
        });
    }

    private static void ConfigureAudit<T>(EntityTypeBuilder<T> entity, string tableName) where T : BaseAuditableEntity
    {
        entity.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone").IsRequired();
        entity.Property(x => x.LastActionAtUtc).HasColumnType("timestamp with time zone").IsRequired().IsConcurrencyToken();
        entity.Property(x => x.LastAction).HasColumnType("character varying(1)").HasMaxLength(1).IsRequired().IsConcurrencyToken();
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.LastActionByUserId).OnDelete(DeleteBehavior.Restrict);
        entity.HasQueryFilter("SoftDelete", x => x.LastAction != "D");
        entity.ToTable(table =>
        {
            table.HasCheckConstraint($"CK_{tableName}_LastAction", "\"LastAction\" IN ('I', 'U', 'D')");
            table.HasCheckConstraint($"CK_{tableName}_AuditTime", "\"LastActionAtUtc\" >= \"CreatedAtUtc\"");
        });
    }
}
