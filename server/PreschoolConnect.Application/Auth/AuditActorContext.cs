namespace PreschoolConnect.Application.Auth;

public sealed record AuditActor(Guid? UserId, string? SystemOperation);

public interface IAuditActorProvider
{
    AuditActor GetActor();
}

public sealed class AuditActorContext
{
    public string? SystemOperation { get; private set; }

    public IDisposable BeginSystemOperation(string operation)
    {
        if (string.IsNullOrWhiteSpace(operation) || operation.Length > 100)
            throw new ArgumentException("A system operation name of 1-100 characters is required.", nameof(operation));
        var previous = SystemOperation;
        SystemOperation = operation;
        return new Scope(() => SystemOperation = previous);
    }

    private sealed class Scope(Action close) : IDisposable
    {
        private bool disposed;
        public void Dispose() { if (!disposed) { disposed = true; close(); } }
    }
}
