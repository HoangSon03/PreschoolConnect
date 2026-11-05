using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PreschoolConnect.Application.Classrooms;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Api.Auth;

public static class ClassroomEndpoints
{
    public static void MapClassroomDetails(this WebApplication app)
    {
        var group = app.MapGroup("/api/classes").RequireAuthorization();
        group.AddEndpointFilter(async (context, next) =>
        {
            var http = context.HttpContext;
            if (!Guid.TryParse(http.User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Results.Unauthorized();
            Guid? selected = null;
            if (http.Request.Headers.TryGetValue("X-School-Id", out var school))
            {
                if (!Guid.TryParse(school, out var parsed))
                    return Results.BadRequest(new { message = "X-School-Id must be a valid school identifier." });
                selected = parsed;
            }
            var resolution = await http.RequestServices.GetRequiredService<SchoolContextResolver>()
                .ResolveAsync(userId, selected, http.RequestAborted);
            if (resolution.Failure == SchoolContextFailure.SchemaNotReady)
                return Results.Json(new { message = "Database migrations are pending." }, statusCode: 503);
            if (resolution.Failure == SchoolContextFailure.SelectionRequired)
                return Results.Conflict(new { message = "Select an active school membership using X-School-Id." });
            if (resolution.Context is not { } member) return Results.Forbid();
            var admin = member.Roles.Contains(SchoolRoleNames.SchoolAdmin);
            if (!admin && !member.Roles.Contains(SchoolRoleNames.Teacher)) return Results.Forbid();
            if (!HttpMethods.IsGet(http.Request.Method))
            {
                if (!admin) return Results.Forbid();
                if (!await http.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(http))
                    return Results.BadRequest(new { message = "A valid antiforgery token is required." });
            }
            http.Items[typeof(ClassroomScope)] = new ClassroomScope(member.SchoolId, userId, admin);
            http.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, IClassroomService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync((ClassroomScope)http.Items[typeof(ClassroomScope)]!, id, ct)));
        group.MapPut("/{id:guid}", async (Guid id, ClassroomUpdate request, HttpContext http,
            IClassroomService service, CancellationToken ct) =>
            Results.Ok(await service.UpdateAsync((ClassroomScope)http.Items[typeof(ClassroomScope)]!, id, request, ct)));
    }
}

public sealed class ClassroomExceptionHandler(ILogger<ClassroomExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string message;
        if (exception is ClassroomException classroom)
        {
            status = classroom.Failure switch { ClassroomFailure.Invalid => 400, ClassroomFailure.NotFound => 404,
                ClassroomFailure.Forbidden => 403, _ => 409 };
            message = classroom.Message;
        }
        else if (context.Request.Path.StartsWithSegments("/api/classes") &&
            (exception is DbUpdateConcurrencyException ||
                exception is DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" or "23514" } }))
        {
            status = 409; message = "Lớp học hoặc dữ liệu liên quan đã thay đổi hay bị trùng. Vui lòng tải lại.";
        }
        else return false;
        logger.LogWarning("Classroom request rejected: {Status} {Reason}", status, message);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message }, cancellationToken);
        return true;
    }
}
