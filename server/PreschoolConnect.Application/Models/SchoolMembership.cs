namespace PreschoolConnect.Application.Models;

public class SchoolMembership
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid UserId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public ICollection<SchoolMembershipRole> Roles { get; set; } = new List<SchoolMembershipRole>();
}
