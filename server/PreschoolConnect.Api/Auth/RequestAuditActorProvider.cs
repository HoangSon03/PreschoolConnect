using System.Security.Claims;
using PreschoolConnect.Application.Auth;

namespace PreschoolConnect.Api.Auth;

public sealed class RequestAuditActorProvider(IHttpContextAccessor accessor, AuditActorContext context) : IAuditActorProvider
{
    public AuditActor GetActor()
    {
        var principal = accessor.HttpContext?.User;
        if (principal?.Identity?.IsAuthenticated == true &&
            Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return new(userId, null);
        if (accessor.HttpContext is null && context.SystemOperation is not null)
            return new(null, context.SystemOperation);
        throw new InvalidOperationException("Business writes require an authenticated actor or an explicitly scoped system operation.");
    }
}
