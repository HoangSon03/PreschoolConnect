namespace PreschoolConnect.Application.Models;

public abstract class BaseAuditableEntity
{
    public DateTimeOffset CreatedAtUtc { get; private set; }
    public Guid? CreatedByUserId { get; private set; }
    public string LastAction { get; private set; } = "I";
    public Guid? LastActionByUserId { get; private set; }
    public DateTimeOffset LastActionAtUtc { get; private set; }
}
