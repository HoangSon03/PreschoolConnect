namespace PreschoolConnect.Application.Models;

public class SchoolMembership : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;
    public ICollection<SchoolMembershipRole> Roles { get; set; } = new List<SchoolMembershipRole>();
}
