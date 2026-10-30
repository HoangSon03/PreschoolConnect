using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddProblemDetails();

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? "Host=localhost;Port=5432;Database=preschoolconnect;Username=postgres";
var cookieSecurePolicy = builder.Environment.IsDevelopment()
    ? CookieSecurePolicy.SameAsRequest
    : CookieSecurePolicy.Always;

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseNpgsql(connectionString));
builder.Services.AddScoped<SchoolContextResolver>();

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

    var schoolYears = await db.SchoolYears
        .AsNoTracking()
        .Where(year => year.SchoolId == access.Context!.SchoolId)
        .OrderBy(year => year.StartDate)
        .Select(year => new { year.Id, year.SchoolId, year.Name, year.StartDate, year.EndDate })
        .ToListAsync(cancellationToken);
    return Results.Ok(schoolYears);
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
    if (string.IsNullOrWhiteSpace(name) || name.Length > 100 || request.EndDate <= request.StartDate)
    {
        return Results.ValidationProblem(new Dictionary<string, string[]>
        {
            [nameof(request.Name)] = string.IsNullOrWhiteSpace(name) || name.Length > 100
                ? ["Name is required and must be at most 100 characters."]
                : [],
            [nameof(request.EndDate)] = request.EndDate <= request.StartDate
                ? ["End date must be after start date."]
                : []
        }.Where(entry => entry.Value.Length > 0).ToDictionary(entry => entry.Key, entry => entry.Value));
    }

    var schoolYear = new SchoolYear
    {
        Id = Guid.NewGuid(),
        SchoolId = access.Context!.SchoolId,
        Name = name,
        StartDate = request.StartDate,
        EndDate = request.EndDate
    };
    db.SchoolYears.Add(schoolYear);
    await db.SaveChangesAsync(cancellationToken);

    return Results.Created($"/api/school-years/{schoolYear.Id}", new
    {
        schoolYear.Id,
        schoolYear.SchoolId,
        schoolYear.Name,
        schoolYear.StartDate,
        schoolYear.EndDate
    });
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
    db.Classrooms.Add(classroom);
    await db.SaveChangesAsync(cancellationToken);

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

public partial class Program;

public sealed record SchoolYearRequest(string Name, DateOnly StartDate, DateOnly EndDate);

public sealed record ClassroomRequest(Guid SchoolYearId, string Name);
