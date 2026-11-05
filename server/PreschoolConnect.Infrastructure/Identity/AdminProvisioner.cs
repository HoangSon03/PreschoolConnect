using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Infrastructure.Identity;

public sealed class AdminProvisioner(ApplicationDbContext db, UserManager<ApplicationUser> users, AuditActorContext actor)
{
    public async Task ProvisionAsync(string email, string password, Guid schoolId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 256 || !new EmailAddressAttribute().IsValid(email) ||
            string.IsNullOrWhiteSpace(password) || password.Length > 1024)
            throw new InvalidOperationException("Configure a valid Bootstrap:AdminEmail and Bootstrap:AdminPassword.");
        using var operation = actor.BeginSystemOperation("initial-admin-provisioning");
        if (!await db.Schools.AnyAsync(school => school.Id == schoolId && school.IsActive, cancellationToken))
            throw new InvalidOperationException("Bootstrap:SchoolId must identify an existing active school.");
        if (await users.FindByEmailAsync(email) is not null)
            throw new InvalidOperationException("Account already exists; provisioning never resets or elevates an existing account.");

        await using var transaction = db.Database.IsRelational()
            ? await db.Database.BeginTransactionAsync(cancellationToken) : null;
        var user = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, IsActive = true };
        var result = await users.CreateAsync(user, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
        var membership = new SchoolMembership { Id = Guid.NewGuid(), UserId = user.Id, SchoolId = schoolId, IsActive = true };
        membership.Roles.Add(new SchoolMembershipRole { MembershipId = membership.Id, Role = SchoolRoleNames.SchoolAdmin });
        db.SchoolMemberships.Add(membership);
        await db.SaveChangesAsync(cancellationToken);
        if (transaction is not null) await transaction.CommitAsync(cancellationToken);
    }
}
