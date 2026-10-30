
using InventorySystem.Backend.Data;
using InventorySystem.Backend.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace InventorySystem.Backend.Setup;

public static class BootstrapAdmin
{
    public static async Task RunAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();

        var db = scope.ServiceProvider
            .GetRequiredService<InventoryDbContext>();

        var hasher = scope.ServiceProvider
            .GetRequiredService<IPasswordHasher<AppUser>>();

        // Only initialize a completely empty database.
        if (await db.BusinessTenants.AnyAsync() ||
            await db.AppUsers.AnyAsync())
        {
            Console.WriteLine(
                "Bootstrap refused: business tenants or users already exist.");
            return;
        }

        Console.Write("Business name: ");
        var businessName = Console.ReadLine()?.Trim();

        Console.Write("Administrator email: ");
        var email = Console.ReadLine()?.Trim();

        if (string.IsNullOrWhiteSpace(businessName) ||
            string.IsNullOrWhiteSpace(email) ||
            !new System.ComponentModel.DataAnnotations.EmailAddressAttribute()
                .IsValid(email))
        {
            Console.WriteLine("A valid business name and email are required.");
            return;
        }

        Console.Write("Administrator password: ");
        var password = ReadPassword();

        if (password.Length < 8)
        {
            Console.WriteLine(
                "\nPassword must be at least 12 characters long.");
            return;
        }

        var tenant = new BusinessTenant
        {
            BusinessName = businessName
        };

        var user = new AppUser
        {
            Tenant = tenant,
            Email = email,
            Role = "Admin",
            IsActive = true
        };

        user.PasswordHash = hasher.HashPassword(user, password);

        db.BusinessTenants.Add(tenant);
        db.AppUsers.Add(user);

        await using var transaction =
            await db.Database.BeginTransactionAsync();

        try
        {
            await db.SaveChangesAsync();
            await transaction.CommitAsync();

            Console.WriteLine("\nBootstrap completed successfully.");
            Console.WriteLine($"Business: {tenant.BusinessName}");
            Console.WriteLine($"Administrator: {user.Email}");
            Console.WriteLine("Role: Admin");
            Console.WriteLine("The password was stored as a hash.");
        }
        catch
        {
            await transaction.RollbackAsync();
            Console.WriteLine(
                "\nBootstrap failed. No success was reported.");
            throw;
        }
        finally
        {
            // Reduce how long the password remains in this variable.
            password = string.Empty;
        }
    }

    private static string ReadPassword()
    {
        var password = new System.Text.StringBuilder();

        while (true)
        {
            var key = Console.ReadKey(intercept: true);

            if (key.Key == ConsoleKey.Enter)
                break;

            if (key.Key == ConsoleKey.Backspace)
            {
                if (password.Length > 0)
                    password.Length--;

                continue;
            }

            if (!char.IsControl(key.KeyChar))
                password.Append(key.KeyChar);
        }

        Console.WriteLine();
        return password.ToString();
    }
}
