using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Infrastructure.Identity;

public sealed class AuthenticationService(
    UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn,
    ApplicationDbContext db) : IAuthenticationService
{
    public async Task<AuthenticatedLogin?> LoginAsync(string email, string password, CancellationToken cancellationToken)
    {
        var user = await users.FindByEmailAsync(email.Trim());
        if (user is null || !user.IsActive || user.TwoFactorEnabled) return null;
        var result = await signIn.CheckPasswordSignInAsync(user, password, lockoutOnFailure: true);
        if (!result.Succeeded) return null;
        var stamp = await users.GetSecurityStampAsync(user);
        var account = await GetUserAsync(user.Id, cancellationToken);
        return account is null ? null : new(account, stamp);
    }

    public async Task<AuthenticatedUser?> GetUserAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (db.Database.IsNpgsql() && (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
            throw new AuthenticationSchemaNotReadyException();
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null || !user.IsActive) return null;
        var roles = await db.SchoolMembershipRoles.AsNoTracking()
            .Where(role => db.SchoolMemberships.Any(member => member.Id == role.MembershipId &&
                member.UserId == userId && member.IsActive &&
                db.Schools.Any(school => school.Id == member.SchoolId && school.IsActive)))
            .Select(role => role.Role).Distinct().ToListAsync(cancellationToken);
        // Claims describe available roles, never ownership of a selected school.
        var aliases = roles.Select(role => role == SchoolRoleNames.SchoolAdmin ? "Admin" : role)
            .Where(role => role is "Admin" or "Teacher" or "Parent").Distinct().Order().ToArray();
        return new(user.Id, user.Email!, string.Join(' ', new[] { user.FirstName, user.LastName }
            .Where(value => !string.IsNullOrWhiteSpace(value))), user.IsActive, aliases);
    }

    public async Task<string?> GetSecurityStampAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        return user is { IsActive: true } ? await users.GetSecurityStampAsync(user) : null;
    }

    public async Task<bool> LogoutAsync(Guid userId)
    {
        var user = await users.FindByIdAsync(userId.ToString());
        if (user is null) return false;
        var result = await users.UpdateSecurityStampAsync(user);
        if (!result.Succeeded)
            throw new InvalidOperationException("Unable to invalidate the account's authentication sessions.");
        return true;
    }
}
