using Microsoft.AspNetCore.Diagnostics;
using PreschoolConnect.Application.Auth;

namespace PreschoolConnect.Api.Auth;

public sealed class AuthenticationExceptionHandler(ILogger<AuthenticationExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        if (exception is not AuthenticationSchemaNotReadyException) return false;
        logger.LogWarning("Authentication unavailable because database migrations are pending.");
        context.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;
        await context.Response.WriteAsJsonAsync(new { message = exception.Message }, cancellationToken);
        return true;
    }
}
