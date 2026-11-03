namespace PreschoolConnect.Application.Models;

public class StudentAccount : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public Guid SchoolId { get; set; }
    public Guid StudentId { get; set; }
    public Guid UserId { get; set; }
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;
}
