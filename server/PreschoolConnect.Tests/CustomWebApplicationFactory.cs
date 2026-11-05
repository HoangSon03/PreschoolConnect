using System.Linq;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using PreschoolConnect.Application.Identity;
using PreschoolConnect.Infrastructure.Persistence;

namespace PreschoolConnect.Tests;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly string _databaseName = $"PreschoolConnectTests-{Guid.NewGuid():N}";
    public bool UseInMemoryDatabase { get; set; } = true;
    public bool EnableDevelopmentSeed { get; set; } = true;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Seed:Enabled"] = EnableDevelopmentSeed.ToString(),
                ["Jwt:Issuer"] = "preschool-tests",
                ["Jwt:Audience"] = "preschool-test-client",
                ["Jwt:SigningKey"] = "synthetic-tests-only-signing-key-not-for-deployment",
                ["ConnectionStrings:DefaultConnection"] =
                    "Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Timeout=1;Command Timeout=1"
            }));

        builder.ConfigureServices(services =>
        {
            services.AddControllers().AddApplicationPart(typeof(AuthRoleTestController).Assembly);
            services.RemoveAll(typeof(DbContextOptions<ApplicationDbContext>));
            services.RemoveAll(typeof(ApplicationDbContext));
            services.RemoveAll(typeof(IDbContextOptionsConfiguration<ApplicationDbContext>));

            if (UseInMemoryDatabase)
            {
                services.AddDbContext<ApplicationDbContext>(options => options.UseInMemoryDatabase(_databaseName));
            }
            else
            {
                services.AddDbContext<ApplicationDbContext>(options =>
                    options.UseNpgsql("Host=127.0.0.1;Port=1;Database=unavailable;Username=test;Timeout=1;Command Timeout=1"));
                return;
            }

            var provider = services.BuildServiceProvider();

            using var scope = provider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            db.Database.EnsureCreated();

            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
            if (!roleManager.RoleExistsAsync("SchoolAdmin").GetAwaiter().GetResult())
            {
                roleManager.CreateAsync(new ApplicationRole { Name = "SchoolAdmin" }).GetAwaiter().GetResult();
            }

            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            if (userManager.FindByEmailAsync("inactive@northside.test").GetAwaiter().GetResult() is null)
            {
                var inactive = new ApplicationUser
                {
                    UserName = "inactive@northside.test",
                    Email = "inactive@northside.test",
                    IsActive = false,
                    EmailConfirmed = true
                };

                userManager.CreateAsync(inactive, "InactivePass!23").GetAwaiter().GetResult();
            }
        });
    }
}
