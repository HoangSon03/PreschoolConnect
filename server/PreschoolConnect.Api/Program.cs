using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PreschoolConnect.Api;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;
using PreschoolConnect.Api.Auth;
using PreschoolConnect.Infrastructure.Identity;
using PreschoolConnect.Application.Teachers;
using PreschoolConnect.Application.Students;
using PreschoolConnect.Application.Classrooms;

var migrationCommand = args.Contains("--migrate", StringComparer.Ordinal);
var resetAdminPasswordCommand = args.Contains("--reset-local-admin-password", StringComparer.Ordinal);
var provisionAdminCommand = args.Contains("--provision-local-admin", StringComparer.Ordinal);
if (new[] { migrationCommand, resetAdminPasswordCommand, provisionAdminCommand }.Count(value => value) > 1)
    throw new InvalidOperationException("Run provisioning, password reset and migration commands separately.");
var builderArgs = args.Where(argument => argument is not "--migrate" and not "--reset-local-admin-password" and not "--provision-local-admin").ToArray();
var builder = WebApplication.CreateBuilder(builderArgs);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AuthenticationExceptionHandler>();
builder.Services.AddExceptionHandler<TeacherExceptionHandler>();
builder.Services.AddExceptionHandler<StudentExceptionHandler>();
builder.Services.AddExceptionHandler<ClassroomExceptionHandler>();
builder.Services.AddExceptionHandler<AuditPolicyExceptionHandler>();
builder.Services.AddSingleton<DatabaseStartupOperations>();

var connectionString = migrationCommand
    ? builder.Configuration.GetConnectionString("MigrationConnection")
        ?? builder.Configuration.GetConnectionString("DefaultConnection")
    : builder.Configuration.GetConnectionString("DefaultConnection");
if (string.IsNullOrWhiteSpace(connectionString))
{
    var requiredConnection = migrationCommand ? "MigrationConnection or DefaultConnection" : "DefaultConnection";
    throw new InvalidOperationException($"ConnectionStrings:{requiredConnection} must be configured.");
}
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<SchoolContextResolver>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditActorContext>();
builder.Services.AddScoped<IAuditActorProvider, RequestAuditActorProvider>();

builder.Services.AddIdentityCore<ApplicationUser>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
        options.User.RequireUniqueEmail = true;
    })
    .AddRoles<ApplicationRole>()
    .AddSignInManager()
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.AddScoped<IAuthenticationService, AuthenticationService>();
builder.Services.AddScoped<AdminProvisioner>();
builder.Services.AddScoped<ITeacherService, TeacherService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<IStudentService, StudentService>();
builder.Services.AddScoped<IClassroomService, ClassroomService>();
builder.Services.AddAuthentication();
if (!migrationCommand && !resetAdminPasswordCommand && !provisionAdminCommand)
    builder.Services.AddJwtAuthentication(builder.Configuration);

builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "PreschoolConnect.Antiforgery";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Strict;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
});
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("login", httpContext =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0,
                AutoReplenishment = true
            }));
});

var app = builder.Build();

if (provisionAdminCommand)
{
    if (!app.Environment.IsDevelopment() ||
        new NpgsqlConnectionStringBuilder(connectionString).Host is not ("127.0.0.1" or "localhost" or "::1") ||
        !app.Configuration.GetValue("Bootstrap:ConfirmDisposableTarget", false))
        throw new InvalidOperationException("Provisioning requires Development, loopback PostgreSQL and Bootstrap:ConfirmDisposableTarget=true.");
    if (!Guid.TryParse(app.Configuration["Bootstrap:SchoolId"], out var schoolId))
        throw new InvalidOperationException("Configure Bootstrap:SchoolId.");
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AdminProvisioner>().ProvisionAsync(
        app.Configuration["Bootstrap:AdminEmail"] ?? "", app.Configuration["Bootstrap:AdminPassword"] ?? "", schoolId);
    app.Logger.LogInformation("Initial administrator provisioned; no HTTP server started.");
    return;
}

if (resetAdminPasswordCommand)
{
    if (!app.Environment.IsDevelopment() ||
        new NpgsqlConnectionStringBuilder(connectionString).Host is not ("127.0.0.1" or "localhost" or "::1"))
        throw new InvalidOperationException("Local password reset requires Development and a loopback PostgreSQL target.");
    await using var scope = app.Services.CreateAsyncScope();
    await LocalAdminPasswordReset.RunAsync(scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>());
    return;
}

if (migrationCommand)
{
    await app.Services.GetRequiredService<DatabaseStartupOperations>().ApplyMigrationsAsync();
    return;
}

async Task<(SchoolContext? Context, IResult? Error)> ResolveRequestTenantAsync(
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver,
    CancellationToken cancellationToken,
    bool requireSchoolAdmin = false)
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null || !user.IsActive)
    {
        return (null, Results.Unauthorized());
    }

    Guid? selectedSchoolId = null;
    if (httpContext.Request.Headers.TryGetValue("X-School-Id", out var schoolHeader))
    {
        if (!Guid.TryParse(schoolHeader.ToString(), out var parsedSchoolId))
        {
            return (null, Results.BadRequest(new { message = "X-School-Id must be a valid school identifier." }));
        }

        selectedSchoolId = parsedSchoolId;
    }

    var resolution = await schoolContextResolver.ResolveAsync(user.Id, selectedSchoolId, cancellationToken);
    if (resolution.Failure == SchoolContextFailure.SchemaNotReady)
        return (null, Results.Json(new { message = "Database migrations are pending. An administrator must complete schema initialization before school access." },
            statusCode: StatusCodes.Status503ServiceUnavailable));
    if (resolution.Failure == SchoolContextFailure.SelectionRequired)
    {
        return (null, Results.Conflict(new { message = "Select one of your active school memberships using X-School-Id." }));
    }

    if (resolution.Context is null)
    {
        return (null, Results.Forbid());
    }

    if (requireSchoolAdmin && !resolution.Context.Roles.Contains(SchoolRoleNames.SchoolAdmin, StringComparer.Ordinal))
    {
        return (null, Results.Forbid());
    }

    return (resolution.Context, null);
}

IQueryable<Classroom> VisibleClassrooms(ApplicationDbContext db, SchoolContext context, HttpContext http, bool includeDeleted = false)
{
    var admin = context.Roles.Contains(SchoolRoleNames.SchoolAdmin);
    var userId = Guid.Parse(http.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value);
    return ClassroomQueries.Visible(db, new(context.SchoolId, userId, admin), includeDeleted);
}

app.UseExceptionHandler(new ExceptionHandlerOptions
{
    StatusCodeSelector = exception => exception is BadHttpRequestException badRequest
        ? badRequest.StatusCode : StatusCodes.Status500InternalServerError
});
app.UseRouting();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/api/health", () => Results.Ok(new
{
    status = "ok",
    timestamp = DateTimeOffset.UtcNow,
    environment = app.Environment.EnvironmentName,
    database = "postgresql"
}));

app.MapGet("/api/health/ready", async (ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var canConnect = await db.Database.CanConnectAsync(cancellationToken);
    return canConnect
        ? Results.Ok(new
        {
            status = "ready",
            timestamp = DateTimeOffset.UtcNow,
            database = "connected"
        })
        : Results.Json(new
        {
            status = "not_ready",
            timestamp = DateTimeOffset.UtcNow,
            database = "unavailable"
        }, statusCode: StatusCodes.Status503ServiceUnavailable);
});

app.MapGet("/api/auth/csrf", (IAntiforgery antiforgery, HttpContext httpContext) =>
{
    var tokens = antiforgery.GetAndStoreTokens(httpContext);
    return Results.Ok(new { requestToken = tokens.RequestToken });
});

app.MapAuthenticationEndpoints();
app.MapTeacherEndpoints();
app.MapStudentEndpoints();
app.MapClassroomDetails();

app.MapGet("/api/tenant/context", [Authorize] async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver,
    CancellationToken cancellationToken) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null || !user.IsActive)
    {
        return Results.Unauthorized();
    }

    Guid? selectedSchoolId = null;
    if (httpContext.Request.Headers.TryGetValue("X-School-Id", out var schoolHeader))
    {
        if (!Guid.TryParse(schoolHeader.ToString(), out var parsedSchoolId))
        {
            return Results.BadRequest(new { message = "X-School-Id must be a valid school identifier." });
        }

        selectedSchoolId = parsedSchoolId;
    }

    var resolution = await schoolContextResolver.ResolveAsync(user.Id, selectedSchoolId, cancellationToken);
    if (resolution.Failure == SchoolContextFailure.SchemaNotReady)
        return Results.Json(new { message = "Database migrations are pending. An administrator must complete schema initialization before school access." },
            statusCode: StatusCodes.Status503ServiceUnavailable);
    if (resolution.Failure == SchoolContextFailure.SelectionRequired)
    {
        return Results.Conflict(new { message = "Select one of your active school memberships using X-School-Id." });
    }

    if (resolution.Context is null)
    {
        return Results.Forbid();
    }

    return Results.Ok(new
    {
        schoolId = resolution.Context.SchoolId,
        membershipId = resolution.Context.MembershipId,
        roles = resolution.Context.Roles
    });
}).RequireAuthorization();

app.MapGet("/api/school-years", [Authorize] async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver,
    ApplicationDbContext db,
    CancellationToken cancellationToken) =>
{
    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken);
    if (access.Error is not null)
    {
        return access.Error;
    }

    var classCounts = await VisibleClassrooms(db, access.Context!, httpContext, includeDeleted: true)
        .GroupBy(classroom => classroom.SchoolYearId)
        .Select(group => new { Id = group.Key, Count = group.Count() })
        .ToDictionaryAsync(item => item.Id, item => item.Count, cancellationToken);
    var schoolYears = await db.SchoolYears
        .AsNoTracking()
        .Where(year => year.SchoolId == access.Context!.SchoolId)
        .OrderBy(year => year.StartDate)
        .Select(year => new { year.Id, year.SchoolId, year.Name, year.StartDate, year.EndDate })
        .ToListAsync(cancellationToken);
    return Results.Ok(schoolYears.Select(year => new { year.Id, year.SchoolId, year.Name, year.StartDate, year.EndDate,
        ClassCount = classCounts.GetValueOrDefault(year.Id) }));
}).RequireAuthorization();

app.MapGet("/api/admin/school-years/deleted", [Authorize] async (
    HttpContext httpContext, UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken, requireSchoolAdmin: true);
    if (access.Error is not null) return access.Error;
    return Results.Ok(await db.SchoolYears.IgnoreQueryFilters().AsNoTracking()
        .Where(year => year.SchoolId == access.Context!.SchoolId && year.LastAction == "D")
        .OrderByDescending(year => year.LastActionAtUtc)
        .Select(year => new { year.Id, year.Name, year.StartDate, year.EndDate, year.CreatedAtUtc,
            year.CreatedByUserId, year.LastAction, year.LastActionByUserId, year.LastActionAtUtc })
        .Take(200).ToListAsync(cancellationToken));
}).RequireAuthorization();

app.MapGet("/api/admin/audit-logs", [Authorize] async (
    HttpContext httpContext, UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver, ApplicationDbContext db, CancellationToken cancellationToken,
    int? page) =>
{
    if (page is < 1 or > 10000) return Results.BadRequest(new { message = "Page must be between 1 and 10000." });
    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken, requireSchoolAdmin: true);
    if (access.Error is not null) return access.Error;
    return Results.Ok(await db.AuditLogs.AsNoTracking().Where(log => log.SchoolId == access.Context!.SchoolId)
        .OrderByDescending(log => log.OccurredAtUtc).ThenBy(log => log.Id)
        .Skip(((page ?? 1) - 1) * 100).Take(100)
        .Select(log => new { log.Id, log.EntityType, log.EntityId, log.Action, log.ActorUserId,
            log.SystemOperation, log.OccurredAtUtc, log.ChangedFields }).ToListAsync(cancellationToken));
}).RequireAuthorization();

app.MapGet("/api/admin/deleted-records/{entityType}", [Authorize] async (
    string entityType, int? page, HttpContext httpContext, UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver, ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    if (page is < 1 or > 10000) return Results.BadRequest(new { message = "Page must be between 1 and 10000." });
    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken, requireSchoolAdmin: true);
    if (access.Error is not null) return access.Error;
    var schoolId = access.Context!.SchoolId;
    var offset = ((page ?? 1) - 1) * 100;
    var records = entityType switch
    {
        "schools" => await DeletedRecordsAsync(db.Schools.IgnoreQueryFilters().Where(item => item.Id == schoolId), offset, cancellationToken),
        "school-years" => await DeletedRecordsAsync(db.SchoolYears.IgnoreQueryFilters().Where(item => item.SchoolId == schoolId), offset, cancellationToken),
        "classrooms" => await DeletedRecordsAsync(db.Classrooms.IgnoreQueryFilters().Where(item => item.SchoolId == schoolId), offset, cancellationToken),
        "students" => await DeletedRecordsAsync(db.Students.IgnoreQueryFilters().Where(item => item.SchoolId == schoolId), offset, cancellationToken),
        "memberships" => await DeletedRecordsAsync(db.SchoolMemberships.IgnoreQueryFilters().Where(item => item.SchoolId == schoolId), offset, cancellationToken),
        "student-accounts" => await DeletedRecordsAsync(db.StudentAccounts.IgnoreQueryFilters().Where(item => item.SchoolId == schoolId), offset, cancellationToken),
        _ => null
    };
    return records is null ? Results.BadRequest(new { message = "Unsupported business entity type." }) : Results.Ok(records);
}).RequireAuthorization();

app.MapPost("/api/school-years", [Authorize] async (
    SchoolYearRequest request,
    HttpContext httpContext,
    IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver,
    ApplicationDbContext db,
    CancellationToken cancellationToken) =>
{
    if (!await antiforgery.IsRequestValidAsync(httpContext))
    {
        return Results.BadRequest(new { message = "A valid antiforgery token is required." });
    }

    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken, requireSchoolAdmin: true);
    if (access.Error is not null)
    {
        return access.Error;
    }

    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || request.StartDate == default || request.EndDate <= request.StartDate)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Name)] = string.IsNullOrWhiteSpace(name) || name.Length > 100
                ? ["Name is required and must be at most 100 characters."]
                : [],
            [nameof(request.EndDate)] = request.EndDate <= request.StartDate
                ? ["End date must be after start date."]
                : [],
            [nameof(request.StartDate)] = request.StartDate == default
                ? ["Start date is required."]
                : []
        }.Where(entry => entry.Value.Length > 0).ToDictionary(entry => entry.Key, entry => entry.Value));
    }

    var schemaError = await SchoolYearSchemaErrorAsync(db, cancellationToken);
    if (schemaError is not null) return schemaError;
    var conflict = await SchoolYearConflictAsync(db, access.Context!.SchoolId, null, name, request.StartDate, request.EndDate, cancellationToken);
    if (conflict is not null) return conflict;

    var schoolYear = new SchoolYear
    {
        Id = Guid.NewGuid(),
        SchoolId = access.Context!.SchoolId,
        Name = name,
        StartDate = request.StartDate,
        EndDate = request.EndDate
    };
    db.SchoolYears.Add(schoolYear);
    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (SchoolYearConstraintMessage(exception) is not null)
    {
        return Results.Conflict(new { message = SchoolYearConstraintMessage(exception) });
    }

    return Results.Created($"/api/school-years/{schoolYear.Id}", new
    {
        schoolYear.Id,
        schoolYear.SchoolId,
        schoolYear.Name,
        schoolYear.StartDate,
        schoolYear.EndDate
    });
}).RequireAuthorization();

app.MapPut("/api/school-years/{id:guid}", [Authorize] async (
    Guid id, SchoolYearRequest request, HttpContext httpContext, IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager, SchoolContextResolver schoolContextResolver,
    ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    if (!await antiforgery.IsRequestValidAsync(httpContext))
        return Results.BadRequest(new { message = "A valid antiforgery token is required." });
    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken, requireSchoolAdmin: true);
    if (access.Error is not null) return access.Error;
    var schoolId = access.Context!.SchoolId;
    var schemaError = await SchoolYearSchemaErrorAsync(db, cancellationToken);
    if (schemaError is not null) return schemaError;
    var year = await db.SchoolYears.SingleOrDefaultAsync(item => item.Id == id && item.SchoolId == schoolId, cancellationToken);
    if (year is null) return Results.NotFound(new { message = "Không tìm thấy năm học trong trường." });
    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || request.StartDate == default || request.EndDate <= request.StartDate)
        return Results.BadRequest(new { message = "Tên năm học phải từ 1–100 ký tự và ngày kết thúc phải sau ngày bắt đầu." });
    var hasClasses = await db.Classrooms.IgnoreQueryFilters().AnyAsync(item => item.SchoolId == schoolId && item.SchoolYearId == id, cancellationToken);
    if (hasClasses && (request.StartDate != year.StartDate || request.EndDate != year.EndDate))
        return Results.Conflict(new { message = "Năm học đã có lớp; chỉ được sửa tên, không được thay đổi ngày." });
    var conflict = await SchoolYearConflictAsync(db, schoolId, id, name, request.StartDate, request.EndDate, cancellationToken);
    if (conflict is not null) return conflict;
    year.Name = name;
    year.StartDate = request.StartDate;
    year.EndDate = request.EndDate;
    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (SchoolYearConstraintMessage(exception) is not null)
    {
        return Results.Conflict(new { message = SchoolYearConstraintMessage(exception) });
    }
    return Results.Ok(new { year.Id, year.SchoolId, year.Name, year.StartDate, year.EndDate });
}).RequireAuthorization();

app.MapDelete("/api/school-years/{id:guid}", [Authorize] async (
    Guid id, HttpContext httpContext, IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager, SchoolContextResolver schoolContextResolver,
    ApplicationDbContext db, CancellationToken cancellationToken) =>
{
    if (!await antiforgery.IsRequestValidAsync(httpContext))
        return Results.BadRequest(new { message = "A valid antiforgery token is required." });
    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken, requireSchoolAdmin: true);
    if (access.Error is not null) return access.Error;
    var schoolId = access.Context!.SchoolId;
    var schemaError = await SchoolYearSchemaErrorAsync(db, cancellationToken);
    if (schemaError is not null) return schemaError;
    var year = await db.SchoolYears.SingleOrDefaultAsync(item => item.Id == id && item.SchoolId == schoolId, cancellationToken);
    if (year is null) return Results.NotFound(new { message = "Không tìm thấy năm học trong trường." });
    if (await db.Classrooms.IgnoreQueryFilters().AnyAsync(item => item.SchoolId == schoolId && item.SchoolYearId == id, cancellationToken))
        return Results.Conflict(new { message = "Không thể xóa năm học đã có lớp. Dữ liệu lớp được giữ nguyên." });
    db.SchoolYears.Remove(year);
    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (SchoolYearConstraintMessage(exception) is not null)
    {
        return Results.Conflict(new { message = SchoolYearConstraintMessage(exception) });
    }
    return Results.NoContent();
}).RequireAuthorization();

app.MapGet("/api/classes", [Authorize] async (
    HttpContext httpContext,
    UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver,
    IClassroomService service,
    CancellationToken cancellationToken) =>
{
    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken);
    if (access.Error is not null)
    {
        return access.Error;
    }

    if (!access.Context!.Roles.Contains(SchoolRoleNames.SchoolAdmin) &&
        !access.Context.Roles.Contains(SchoolRoleNames.Teacher))
        return Results.Forbid();
    httpContext.Response.Headers.CacheControl = "no-store";
    return Results.Ok(await service.ListAsync(new(access.Context.SchoolId,
        Guid.Parse(httpContext.User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)!.Value),
        access.Context.Roles.Contains(SchoolRoleNames.SchoolAdmin)), cancellationToken));
}).RequireAuthorization();

app.MapPost("/api/classes", [Authorize] async (
    ClassroomRequest request,
    HttpContext httpContext,
    IAntiforgery antiforgery,
    UserManager<ApplicationUser> userManager,
    SchoolContextResolver schoolContextResolver,
    ApplicationDbContext db,
    CancellationToken cancellationToken) =>
{
    if (!await antiforgery.IsRequestValidAsync(httpContext))
    {
        return Results.BadRequest(new { message = "A valid antiforgery token is required." });
    }

    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken, requireSchoolAdmin: true);
    if (access.Error is not null)
    {
        return access.Error;
    }

    var name = request.Name?.Trim();
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Name)] = ["Name is required and must be at most 100 characters."]
        });
    }

    var schoolId = access.Context!.SchoolId;
    var schoolYearExists = await db.SchoolYears.AnyAsync(
        year => year.Id == request.SchoolYearId && year.SchoolId == schoolId,
        cancellationToken);
    if (!schoolYearExists)
    {
        return Results.NotFound(new { message = "The school year was not found in the selected school." });
    }

    var normalizedName = name.ToLowerInvariant();
    var sameYearClasses = db.Classrooms.Where(classroom =>
        classroom.SchoolId == schoolId && classroom.SchoolYearId == request.SchoolYearId);
    var duplicateName = db.Database.IsNpgsql()
        ? await sameYearClasses.AnyAsync(classroom =>
            EF.Functions.Collate(classroom.Name.Trim(), "pg_c_utf8").ToLower() == normalizedName, cancellationToken)
        : await sameYearClasses.AnyAsync(classroom => classroom.Name.Trim().ToLower() == normalizedName, cancellationToken);
    if (duplicateName)
    {
        return Results.Conflict(new { message = "A class with this name already exists in the school year (case-insensitive)." });
    }

    var classroom = new Classroom
    {
        Id = Guid.NewGuid(),
        SchoolId = schoolId,
        SchoolYearId = request.SchoolYearId,
        Name = name
    };
    var schemaError = await SchoolYearSchemaErrorAsync(db, cancellationToken);
    if (schemaError is not null) return schemaError;
    db.Classrooms.Add(classroom);
    try
    {
        await db.SaveChangesAsync(cancellationToken);
    }
    catch (DbUpdateException exception) when (SchoolYearConstraintMessage(exception) is not null)
    {
        return Results.Conflict(new { message = SchoolYearConstraintMessage(exception) });
    }

    return Results.Created($"/api/classes/{classroom.Id}", new
    {
        classroom.Id,
        classroom.SchoolId,
        classroom.SchoolYearId,
        classroom.Name
    });
}).RequireAuthorization();

if (app.Environment.IsDevelopment())
{
    await app.Services.GetRequiredService<DatabaseStartupOperations>().InitializeDevelopmentDataAsync();
}

app.MapControllers();
app.Run();

static async Task<IResult?> SchoolYearSchemaErrorAsync(ApplicationDbContext db, CancellationToken cancellationToken)
{
    if (db.Database.IsNpgsql() && (await db.Database.GetPendingMigrationsAsync(cancellationToken)).Any())
        return Results.Json(new { message = "Chưa hoàn tất migration cơ sở dữ liệu. Quản trị viên cần kiểm tra năm học trùng tên/chồng ngày trước khi bật thao tác lưu." }, statusCode: StatusCodes.Status503ServiceUnavailable);
    return null;
}

static async Task<IResult?> SchoolYearConflictAsync(ApplicationDbContext db, Guid schoolId, Guid? id,
    string name, DateOnly start, DateOnly end, CancellationToken cancellationToken)
{
    var normalizedName = name.ToLowerInvariant();
    var years = db.SchoolYears.Where(year => year.SchoolId == schoolId && (!id.HasValue || year.Id != id.Value));
    var duplicateName = db.Database.IsNpgsql()
        ? await years.AnyAsync(year => EF.Functions.Collate(year.Name.Trim(), "pg_c_utf8").ToLower() == normalizedName, cancellationToken)
        : await years.AnyAsync(year => year.Name.Trim().ToLower() == normalizedName, cancellationToken);
    if (duplicateName)
        return Results.Conflict(new { message = "Tên năm học đã tồn tại trong trường (không phân biệt hoa/thường)." });
    if (await years.AnyAsync(year => year.StartDate <= end && year.EndDate >= start, cancellationToken))
        return Results.Conflict(new { message = "Khoảng ngày trùng với năm học khác. Ngày bắt đầu phải sau ngày kết thúc của năm trước." });
    return null;
}

static string? SchoolYearConstraintMessage(DbUpdateException exception) =>
    exception is DbUpdateConcurrencyException ? "Dữ liệu vừa được thay đổi hoặc xóa. Tải lại danh sách và thử lại." :
    exception.InnerException is PostgresException postgres ? postgres.SqlState is "40P01" or "40001"
        ? "Dữ liệu vừa được thay đổi đồng thời. Tải lại danh sách và thử lại."
        : postgres.ConstraintName switch
    {
        "UX_SchoolYears_NormalizedName" => "Tên năm học đã tồn tại trong trường.",
        "CK_SchoolYears_NoOverlap" => "Khoảng ngày trùng với năm học khác.",
        "CK_SchoolYears_DatesInUse" => "Năm học đã có lớp; không được thay đổi ngày hoặc xóa.",
        "CK_Classrooms_ActiveSchoolYear" => "Năm học đã bị xóa; tải lại danh sách trước khi tạo lớp.",
        "UX_Classrooms_NormalizedName" or "IX_Classrooms_SchoolYearId_Name" => "Tên lớp đã tồn tại trong năm học (không phân biệt hoa/thường).",
        "FK_Classrooms_SchoolYears_SchoolId_SchoolYearId" => "Không thể xóa năm học đã có lớp.",
        _ => null
    } : null;

static Task<List<DeletedRecordResponse>> DeletedRecordsAsync<T>(IQueryable<T> query, int offset, CancellationToken cancellationToken) where T : BaseAuditableEntity =>
    query.AsNoTracking().Where(item => item.LastAction == "D").OrderByDescending(item => item.LastActionAtUtc)
        .ThenBy(item => EF.Property<Guid>(item, "Id")).Skip(offset).Take(100)
        .Select(item => new DeletedRecordResponse(EF.Property<Guid>(item, "Id"), item.CreatedAtUtc, item.CreatedByUserId,
            item.LastAction, item.LastActionByUserId, item.LastActionAtUtc)).ToListAsync(cancellationToken);

public partial class Program;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SchoolYearRequest(string Name, DateOnly StartDate, DateOnly EndDate);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ClassroomRequest(Guid SchoolYearId, string Name);

public sealed record DeletedRecordResponse(Guid Id, DateTimeOffset CreatedAtUtc, Guid? CreatedByUserId,
    string LastAction, Guid? LastActionByUserId, DateTimeOffset LastActionAtUtc);
