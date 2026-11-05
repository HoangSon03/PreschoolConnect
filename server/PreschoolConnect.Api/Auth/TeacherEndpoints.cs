using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Application.Teachers;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Api.Auth;

public static class TeacherEndpoints
{
    public static void MapTeacherEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/teachers").RequireAuthorization();
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
            http.Items[typeof(TeacherScope)] = new TeacherScope(member.SchoolId, userId, admin);
            http.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("/", async (HttpContext http, ITeacherService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(Scope(http), ct)));
        group.MapGet("/accounts", async (HttpContext http, ITeacherService service, CancellationToken ct) =>
            Results.Ok(await service.AccountsAsync(Scope(http), ct)));
        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, ITeacherService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(Scope(http), id, ct)));
        group.MapPost("/", async (TeacherRequest request, HttpContext http, ITeacherService service, CancellationToken ct) =>
        {
            var result = await service.SaveAsync(Scope(http), null, request, ct);
            return Results.Created($"/api/teachers/{result.Id}", result);
        });
        group.MapPut("/{id:guid}", async (Guid id, TeacherRequest request, HttpContext http, ITeacherService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAsync(Scope(http), id, request, ct)));
        group.MapPost("/{id:guid}/assignments", async (Guid id, AssignmentRequest request, HttpContext http,
            ITeacherService service, CancellationToken ct) => Results.Ok(await service.AssignAsync(Scope(http), id, request, ct)));
        group.MapDelete("/{id:guid}/assignments/{assignmentId:guid}", async (Guid id, Guid assignmentId, HttpContext http,
            ITeacherService service, CancellationToken ct) => Results.Ok(await service.EndAssignmentAsync(Scope(http), id, assignmentId, ct)));
    }

    private static TeacherScope Scope(HttpContext http) => (TeacherScope)http.Items[typeof(TeacherScope)]!;
}

public sealed class TeacherExceptionHandler(ILogger<TeacherExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string message;
        if (exception is TeacherException teacher)
        {
            status = teacher.Failure switch { TeacherFailure.Invalid => 400, TeacherFailure.NotFound => 404,
                TeacherFailure.Forbidden => 403, _ => 409 };
            message = teacher.Message;
        }
        else if (context.Request.Path.StartsWithSegments("/api/teachers") && exception is DbUpdateConcurrencyException)
        {
            status = 409; message = "Hồ sơ đã thay đổi. Vui lòng tải lại trước khi lưu.";
        }
        else if (context.Request.Path.StartsWithSegments("/api/teachers") &&
            exception is DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" or "23514" } })
        {
            status = 409; message = "Dữ liệu liên kết hoặc phân công đã thay đổi hay bị trùng. Vui lòng tải lại.";
        }
        else return false;
        logger.LogWarning("Teacher request rejected: {Status} {Reason}", status, message);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message }, cancellationToken);
        return true;
    }
}
