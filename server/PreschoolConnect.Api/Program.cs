using System.Threading.RateLimiting;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;
using PreschoolConnect.Api.Auth;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<AuditPolicyExceptionHandler>();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=preschoolconnect;Username=postgres";
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<SchoolContextResolver>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<AuditActorContext>();
builder.Services.AddScoped<IAuditActorProvider, RequestAuditActorProvider>();

builder.Services.AddIdentity<ApplicationUser, ApplicationRole>(options =>
    {
        options.Password.RequireDigit = true;
        options.Password.RequireLowercase = true;
        options.Password.RequireUppercase = true;
        options.Password.RequiredLength = 8;
        options.Password.RequireNonAlphanumeric = false;
        options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(5);
        options.Lockout.MaxFailedAccessAttempts = 5;
        options.Lockout.AllowedForNewUsers = true;
    })
    .AddEntityFrameworkStores<ApplicationDbContext>()
    .AddDefaultTokenProviders();

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.Name = "PreschoolConnect.Auth";
    options.Cookie.HttpOnly = true;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = cookieSecurePolicy;
    options.ExpireTimeSpan = TimeSpan.FromHours(8);
    options.SlidingExpiration = true;
    options.Events.OnRedirectToLogin = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
    options.Events.OnRedirectToAccessDenied = context =>
    {
        if (context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            return Task.CompletedTask;
        }

        context.Response.Redirect(context.RedirectUri);
        return Task.CompletedTask;
    };
});

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

app.MapGet("/api/auth/me", [Authorize] async (HttpContext httpContext, UserManager<ApplicationUser> userManager) =>
{
    var user = await userManager.GetUserAsync(httpContext.User);
    if (user is null)
    {
        return Results.Unauthorized();
    }

    var roles = await userManager.GetRolesAsync(user);
    return Results.Ok(new
    {
        userId = user.Id,
        email = user.Email,
        firstName = user.FirstName,
        lastName = user.LastName,
        fullName = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
        isActive = user.IsActive,
        roles
    });
});

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

    var classCounts = await db.Classrooms.IgnoreQueryFilters().AsNoTracking()
        .Where(classroom => classroom.SchoolId == access.Context!.SchoolId)
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
    ApplicationDbContext db,
    CancellationToken cancellationToken) =>
{
    var access = await ResolveRequestTenantAsync(httpContext, userManager, schoolContextResolver, cancellationToken);
    if (access.Error is not null)
    {
        return access.Error;
    }

    var classes = await db.Classrooms
        .AsNoTracking()
        .Where(classroom => classroom.SchoolId == access.Context!.SchoolId)
        .OrderBy(classroom => classroom.Name)
        .Select(classroom => new { classroom.Id, classroom.SchoolId, classroom.SchoolYearId, classroom.Name })
        .ToListAsync(cancellationToken);
    return Results.Ok(classes);
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

    var duplicateName = await db.Classrooms.AnyAsync(
        classroom => classroom.SchoolYearId == request.SchoolYearId && classroom.Name == name,
        cancellationToken);
    if (duplicateName)
    {
        return Results.Conflict(new { message = "A class with this name already exists in the school year." });
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

app.MapPost("/api/auth/login", async ([FromBody] LoginRequest request, HttpContext httpContext, IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager) =>
{
    if (!await antiforgery.IsRequestValidAsync(httpContext))
    {
        return Results.BadRequest(new { message = "A valid antiforgery token is required." });
    }

    if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
    {
        return Results.BadRequest(new { message = "Email and password are required." });
    }

    var user = await userManager.FindByEmailAsync(request.Email.Trim());
    if (user is null || !user.IsActive)
    {
        return Results.Unauthorized();
    }

    var result = await signInManager.PasswordSignInAsync(user, request.Password, true, true);
    if (!result.Succeeded)
    {
        return Results.Unauthorized();
    }

    var roles = await userManager.GetRolesAsync(user);
    return Results.Ok(new
    {
        message = "Login successful.",
        user = new
        {
            userId = user.Id,
            email = user.Email,
            firstName = user.FirstName,
            lastName = user.LastName,
            fullName = string.Join(' ', new[] { user.FirstName, user.LastName }.Where(x => !string.IsNullOrWhiteSpace(x))),
            isActive = user.IsActive,
            roles
        }
    });
}).RequireRateLimiting("login");

app.MapPost("/api/auth/logout", [Authorize] async (HttpContext httpContext, IAntiforgery antiforgery, SignInManager<ApplicationUser> signInManager) =>
{
    if (!await antiforgery.IsRequestValidAsync(httpContext))
    {
        return Results.BadRequest(new { message = "A valid antiforgery token is required." });
    }

    await signInManager.SignOutAsync();
    return Results.Ok(new { message = "Signed out." });
});

using (var scope = app.Services.CreateScope())
{
    using var systemOperation = scope.ServiceProvider.GetRequiredService<AuditActorContext>().BeginSystemOperation("development-initialization");
    var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DatabaseInitializer");
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync();

        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

        if (app.Environment.IsDevelopment())
        {
            var school = await db.Schools.FirstOrDefaultAsync(x => x.Slug == "northside");
            if (school is null)
            {
                school = new School { Id = Guid.NewGuid(), Name = "Northside Academy", Slug = "northside", IsActive = true };
                db.Schools.Add(school);
                await db.SaveChangesAsync();
            }

            if (!await roleManager.RoleExistsAsync("SchoolAdmin"))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = "SchoolAdmin", SchoolId = school.Id });
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(x => x.Description)));
                }
            }

            var adminPassword = app.Configuration["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(adminPassword))
            {
                logger.LogInformation("Skipping development administrator seed; configure Seed__AdminPassword to opt in.");
            }
            else
            {
                const string adminEmail = "admin@northside.test";
                var adminUser = await userManager.FindByEmailAsync(adminEmail);
                if (adminUser is null)
                {
                    adminUser = new ApplicationUser
                    {
                        Id = Guid.NewGuid(),
                        UserName = adminEmail,
                        Email = adminEmail,
                        FirstName = "System",
                        LastName = "Administrator",
                        SchoolId = school.Id,
                        IsActive = true,
                        EmailConfirmed = true,
                    };

                    var createResult = await userManager.CreateAsync(adminUser, adminPassword);
                    if (!createResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join("; ", createResult.Errors.Select(x => x.Description)));
                    }
                }

                if (!await userManager.IsInRoleAsync(adminUser, "SchoolAdmin"))
                {
                    var roleResult = await userManager.AddToRoleAsync(adminUser, "SchoolAdmin");
                    if (!roleResult.Succeeded)
                    {
                        throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(x => x.Description)));
                    }
                }

                var membership = await db.SchoolMemberships
                    .Include(x => x.Roles)
                    .FirstOrDefaultAsync(x => x.SchoolId == school.Id && x.UserId == adminUser.Id);
                if (membership is null)
                {
                    membership = new SchoolMembership
                    {
                        Id = Guid.NewGuid(),
                        SchoolId = school.Id,
                        UserId = adminUser.Id,
                        IsActive = true
                    };
                    membership.Roles.Add(new SchoolMembershipRole
                    {
                        MembershipId = membership.Id,
                        Role = SchoolRoleNames.SchoolAdmin
                    });
                    db.SchoolMemberships.Add(membership);
                    await db.SaveChangesAsync();
                }
            }
        }

        logger.LogInformation("Database initialized for Preschool Connect.");
    }
    catch (Exception ex)
    {
        if (!app.Environment.IsDevelopment())
        {
            throw;
        }

        logger.LogWarning(ex, "PostgreSQL is unavailable during startup; app will continue with a degraded local configuration.");
    }
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
