using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Models;

namespace PreschoolConnect.Infrastructure.Persistence;

public enum SchoolContextFailure
{
    None,
    NoActiveMembership,
    SelectionRequired,
    MembershipNotFound
}

public sealed record SchoolContext(Guid SchoolId, Guid MembershipId, IReadOnlyList<string> Roles);

public sealed record SchoolContextResolution(SchoolContext? Context, SchoolContextFailure Failure);

public sealed class SchoolContextResolver(ApplicationDbContext db)
{
    public async Task<SchoolContextResolution> ResolveAsync(
        Guid userId,
        Guid? selectedSchoolId,
        CancellationToken cancellationToken)
    {
        var memberships = await db.SchoolMemberships
            .AsNoTracking()
            .Include(membership => membership.Roles)
            .Where(membership =>
                membership.UserId == userId &&
                membership.IsActive &&
                db.Schools.Any(school => school.Id == membership.SchoolId && school.IsActive))
            .ToListAsync(cancellationToken);

        if (memberships.Count == 0)
        {
            return new(null, SchoolContextFailure.NoActiveMembership);
        }

        SchoolMembership? selectedMembership;
        if (selectedSchoolId.HasValue)
        {
            selectedMembership = memberships.SingleOrDefault(membership => membership.SchoolId == selectedSchoolId.Value);
            if (selectedMembership is null)
            {
                return new(null, SchoolContextFailure.MembershipNotFound);
            }
        }
        else if (memberships.Count == 1)
        {
            selectedMembership = memberships[0];
        }
        else
        {
            return new(null, SchoolContextFailure.SelectionRequired);
        }

        return new(
            new SchoolContext(
                selectedMembership.SchoolId,
                selectedMembership.Id,
                selectedMembership.Roles.Select(role => role.Role).Order().ToArray()),
            SchoolContextFailure.None);
    }
}
