using Microsoft.AspNetCore.Diagnostics;
using PreschoolConnect.Application.Models;

namespace PreschoolConnect.Api.Auth;

public sealed class AuditPolicyExceptionHandler(ILogger<AuditPolicyExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AuditPolicyException policy) return false;
        logger.LogWarning("Business audit policy rejected {Method} {Path}: {Reason}",
            context.Request.Method, context.Request.Path, policy.Message);
        context.Response.StatusCode = StatusCodes.Status409Conflict;
        await context.Response.WriteAsJsonAsync(new { message = policy.Message }, cancellationToken);
        return true;
    }
}
