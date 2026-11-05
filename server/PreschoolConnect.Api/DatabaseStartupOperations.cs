using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using PreschoolConnect.Application.Auth;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Application.Models;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Api;

public sealed class DatabaseStartupOperations(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<DatabaseStartupOperations> logger)
{
    public async Task ApplyMigrationsAsync(CancellationToken cancellationToken = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Database.MigrateAsync(cancellationToken);
    }

    public async Task InitializeDevelopmentDataAsync(CancellationToken cancellationToken = default)
    {
        if (!configuration.GetValue("Seed:Enabled", false))
        {
            logger.LogInformation("Development data initialization disabled by Seed:Enabled.");
            return;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        using var systemOperation = scope.ServiceProvider.GetRequiredService<AuditActorContext>()
            .BeginSystemOperation("development-initialization");

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();

            var school = await db.Schools.FirstOrDefaultAsync(item => item.Slug == "northside", cancellationToken);
            if (school is null)
            {
                school = new School { Id = Guid.NewGuid(), Name = "Northside Academy", Slug = "northside", IsActive = true };
                db.Schools.Add(school);
                await db.SaveChangesAsync(cancellationToken);
            }

            if (!await roleManager.RoleExistsAsync("SchoolAdmin"))
            {
                var roleResult = await roleManager.CreateAsync(new ApplicationRole { Name = "SchoolAdmin", SchoolId = school.Id });
                if (!roleResult.Succeeded)
                {
                    throw new InvalidOperationException(string.Join("; ", roleResult.Errors.Select(error => error.Description)));
                }
            }

            logger.LogInformation("Development data initialized for Preschool Connect.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Development data initialization failed; the API will continue without seeded data.");
        }
    }
}
