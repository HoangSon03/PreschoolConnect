using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;

namespace PreschoolConnect.Infrastructure.Persistence;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>
{
    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
    {
    }

    public DbSet<School> Schools => Set<School>();
    public DbSet<SchoolMembership> SchoolMemberships => Set<SchoolMembership>();
    public DbSet<SchoolMembershipRole> SchoolMembershipRoles => Set<SchoolMembershipRole>();
    public DbSet<SchoolYear> SchoolYears => Set<SchoolYear>();
    public DbSet<Classroom> Classrooms => Set<Classroom>();
    public DbSet<Student> Students => Set<Student>();
    public DbSet<StudentAccount> StudentAccounts => Set<StudentAccount>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<School>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.Slug).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(200).IsRequired();
            entity.Property(x => x.Slug).HasMaxLength(100).IsRequired();
        });

        builder.Entity<SchoolMembership>(entity =>
        {
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
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<SchoolYear>(entity =>
        {
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
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SchoolYearId, x.Name }).IsUnique();
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
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.SchoolId, x.LastName, x.FirstName }).IsUnique(false);
            entity.Property(x => x.FirstName).HasMaxLength(100).IsRequired();
            entity.Property(x => x.LastName).HasMaxLength(100).IsRequired();
            entity.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<StudentAccount>(entity =>
        {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => x.UserId).IsUnique();
            entity.HasIndex(x => x.StudentId).IsUnique();
            entity.HasIndex(x => new { x.SchoolId, x.StudentId }).IsUnique();
            entity.HasOne<School>().WithMany().HasForeignKey(x => x.SchoolId).OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Student>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
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
    }
}
