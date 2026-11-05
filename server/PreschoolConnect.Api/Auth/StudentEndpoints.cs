using System.Security.Claims;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Application.Students;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Api.Auth;

public static class StudentEndpoints
{
    public static void MapStudentEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/students").RequireAuthorization();
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
            var teacher = member.Roles.Contains(SchoolRoleNames.Teacher);
            var parent = member.Roles.Contains(SchoolRoleNames.Parent);
            if (!admin && !teacher && !parent) return Results.Forbid();
            if (!HttpMethods.IsGet(http.Request.Method))
            {
                if (!admin) return Results.Forbid();
                if (!await http.RequestServices.GetRequiredService<IAntiforgery>().IsRequestValidAsync(http))
                    return Results.BadRequest(new { message = "A valid antiforgery token is required." });
            }
            http.Items[typeof(StudentScope)] = new StudentScope(member.SchoolId, userId, admin, teacher, parent);
            http.Response.Headers.CacheControl = "no-store";
            return await next(context);
        });
        group.MapGet("/", async (HttpContext http, IStudentService service, CancellationToken ct) =>
            Results.Ok(await service.ListAsync(Scope(http), ct)));
        group.MapGet("/{id:guid}", async (Guid id, HttpContext http, IStudentService service, CancellationToken ct) =>
            Results.Ok(await service.GetAsync(Scope(http), id, ct)));
        group.MapPost("/", async (StudentRequest request, HttpContext http, IStudentService service, CancellationToken ct) =>
        {
            var result = await service.SaveAsync(Scope(http), null, request, ct);
            return Results.Created($"/api/students/{result.Id}", result);
        });
        group.MapPut("/{id:guid}", async (Guid id, StudentRequest request, HttpContext http, IStudentService service, CancellationToken ct) =>
            Results.Ok(await service.SaveAsync(Scope(http), id, request, ct)));
        group.MapPost("/{id:guid}/enrollments", async (Guid id, EnrollmentRequest request, HttpContext http, IStudentService service, CancellationToken ct) =>
            Results.Ok(await service.EnrollAsync(Scope(http), id, request, ct)));
        group.MapDelete("/{id:guid}/enrollments/{enrollmentId:guid}", async (Guid id, Guid enrollmentId, HttpContext http,
            IStudentService service, CancellationToken ct) => Results.Ok(await service.EndAsync(Scope(http), id, enrollmentId, ct)));
    }

    private static StudentScope Scope(HttpContext http) => (StudentScope)http.Items[typeof(StudentScope)]!;
}

public sealed class StudentExceptionHandler(ILogger<StudentExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext context, Exception exception, CancellationToken cancellationToken)
    {
        int status;
        string message;
        if (exception is StudentException student)
        {
            status = student.Failure switch { StudentFailure.Invalid => 400, StudentFailure.NotFound => 404,
                StudentFailure.Forbidden => 403, _ => 409 };
            message = student.Message;
        }
        else if (context.Request.Path.StartsWithSegments("/api/students") &&
            (exception is DbUpdateConcurrencyException ||
             exception is DbUpdateException { InnerException: PostgresException { SqlState: "23505" or "23503" or "23514" or "23P01" } } ||
             exception is PostgresException { SqlState: "23P01" }))
        {
            status = 409; message = "Hồ sơ hoặc lịch sử lớp đã thay đổi hay bị chồng lấn. Vui lòng tải lại.";
        }
        else return false;
        logger.LogWarning("Student request rejected: {Status} {Reason}", status, message);
        context.Response.StatusCode = status;
        await context.Response.WriteAsJsonAsync(new { message }, cancellationToken);
        return true;
    }
}
