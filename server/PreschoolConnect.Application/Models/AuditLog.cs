namespace PreschoolConnect.Application.Models;

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid SchoolId { get; private set; }
    public string EntityType { get; private set; } = string.Empty;
    public Guid EntityId { get; private set; }
    public string Action { get; private set; } = string.Empty;
    public Guid? ActorUserId { get; private set; }
    public string? SystemOperation { get; private set; }
    public DateTimeOffset OccurredAtUtc { get; private set; }
    public string ChangedFields { get; private set; } = "[]";

    public static AuditLog Record(Guid schoolId, string entityType, Guid entityId, string action,
        Guid? actorUserId, string? systemOperation, DateTimeOffset timestamp, string changedFields) => new()
    {
        Id = Guid.NewGuid(), SchoolId = schoolId, EntityType = entityType, EntityId = entityId,
        Action = action, ActorUserId = actorUserId, SystemOperation = systemOperation,
        OccurredAtUtc = timestamp, ChangedFields = changedFields
    };
}
