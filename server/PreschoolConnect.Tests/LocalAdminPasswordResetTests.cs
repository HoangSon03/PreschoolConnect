using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using PreschoolConnect.Api;
using PreschoolConnect.Application.Identity;
using Xunit;

namespace PreschoolConnect.Tests;

public class LocalAdminPasswordResetTests
{
    [Fact]
    public async Task ResetUsesIdentityAndRejectsWeakPasswordsWithoutChangingCredentials()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser
        {
            UserName = LocalAdminPasswordReset.Email, Email = LocalAdminPasswordReset.Email,
            IsActive = true, EmailConfirmed = true
        };
        Assert.True((await users.CreateAsync(user, "SyntheticOld!123")).Succeeded);
        var stamp = user.SecurityStamp;
        await Assert.ThrowsAsync<InvalidOperationException>(() => LocalAdminPasswordReset.ResetAsync(users, "weak"));
        Assert.True(await users.CheckPasswordAsync(user, "SyntheticOld!123"));
        await LocalAdminPasswordReset.ResetAsync(users, "SyntheticNew!456");
        Assert.False(await users.CheckPasswordAsync(user, "SyntheticOld!123"));
        Assert.True(await users.CheckPasswordAsync(user, "SyntheticNew!456"));
        Assert.NotEqual(stamp, user.SecurityStamp);
        Assert.Empty(await users.GetRolesAsync(user));
    }

    [Fact]
    public async Task MissingOrInactiveAccountIsNotCreatedOrActivated()
    {
        using var factory = new CustomWebApplicationFactory { EnableDevelopmentSeed = false };
        using var scope = factory.Services.CreateScope();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => LocalAdminPasswordReset.ResetAsync(users, "SyntheticNew!456"));
        Assert.Null(await users.FindByEmailAsync(LocalAdminPasswordReset.Email));
        var user = new ApplicationUser
        {
            UserName = LocalAdminPasswordReset.Email, Email = LocalAdminPasswordReset.Email, IsActive = false
        };
        Assert.True((await users.CreateAsync(user, "SyntheticOld!123")).Succeeded);
        await Assert.ThrowsAsync<InvalidOperationException>(() => LocalAdminPasswordReset.ResetAsync(users, "SyntheticNew!456"));
        Assert.False(user.IsActive);
        Assert.True(await users.CheckPasswordAsync(user, "SyntheticOld!123"));
    }
}
