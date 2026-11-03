namespace PreschoolConnect.Application.Models;

public class School : BaseAuditableEntity
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedUtc { get; private set; } = DateTimeOffset.UtcNow;
}
