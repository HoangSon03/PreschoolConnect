using System.Text;
using Microsoft.AspNetCore.Identity;
using PreschoolConnect.Application.Identity;

namespace PreschoolConnect.Api;

public static class LocalAdminPasswordReset
{
    public const string Email = "admin@northside.test";

    public static async Task RunAsync(UserManager<ApplicationUser> users)
    {
        if (Console.IsInputRedirected)
            throw new InvalidOperationException("Run this command in an interactive terminal; do not pipe a password.");
        Console.WriteLine($"Reset password for {Email} on the configured local disposable database.");
        Console.Write("Confirm this database contains only disposable synthetic data (type RESET): ");
        if (Console.ReadLine() != "RESET")
            throw new InvalidOperationException("Password reset cancelled; no changes made.");
        var password = ReadPassword("New password: ");
        var confirmation = ReadPassword("Confirm new password: ");
        if (password != confirmation)
            throw new InvalidOperationException("Passwords do not match; no changes made.");
        await ResetAsync(users, password);
        Console.WriteLine("Password reset succeeded. No account, membership, seed or migration was created. Existing cookies are invalidated on the next Identity security-stamp validation.");
    }

    public static async Task ResetAsync(UserManager<ApplicationUser> users, string password)
    {
        var user = await users.FindByEmailAsync(Email);
        if (user is null || !user.IsActive)
            throw new InvalidOperationException("The local administrator account is missing or inactive. No account was created.");
        var token = await users.GeneratePasswordResetTokenAsync(user);
        var result = await users.ResetPasswordAsync(user, token, password);
        if (!result.Succeeded)
            throw new InvalidOperationException(string.Join("; ", result.Errors.Select(error => error.Description)));
    }

    private static string ReadPassword(string prompt)
    {
        Console.Write(prompt);
        var value = new StringBuilder();
        while (true)
        {
            var key = Console.ReadKey(intercept: true);
            if (key.Key == ConsoleKey.Enter)
            {
                Console.WriteLine();
                return value.ToString();
            }
            if (key.Key == ConsoleKey.Escape)
                throw new InvalidOperationException("Password reset cancelled; no changes made.");
            if (key.Key == ConsoleKey.Backspace)
            {
                if (value.Length > 0) value.Length--;
            }
            else if (!char.IsControl(key.KeyChar))
                value.Append(key.KeyChar);
        }
    }
}
