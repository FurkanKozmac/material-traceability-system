using backend.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace backend.Data;

public static class DbInitializer
{
    public static async Task SeedAsync(
        MtsDbContext context,
        IConfiguration configuration,
        bool isDevelopment,
        ILogger? logger = null)
    {
        var demoDataEnabled = ReadBooleanSetting(configuration, "DEMO_DATA_ENABLED", isDevelopment);
        var demoUserEnabled = ReadBooleanSetting(configuration, "DEMO_USER_ENABLED", isDevelopment);
        var adminPasswordReset = ReadBooleanSetting(configuration, "ADMIN_PASSWORD_RESET", false);

        var adminRole = await context.Roles.FirstOrDefaultAsync(role => role.Name == "Admin");
        if (adminRole is null)
        {
            adminRole = new Role { Name = "Admin" };
            context.Roles.Add(adminRole);
        }

        var operatorRole = await context.Roles.FirstOrDefaultAsync(role => role.Name == "Operator");
        if (operatorRole is null)
        {
            operatorRole = new Role { Name = "Operator" };
            context.Roles.Add(operatorRole);
        }
        await context.SaveChangesAsync();

        var demoUser = await context.Users.Include(user => user.Roles)
            .FirstOrDefaultAsync(user => user.Username == "demo");
        if (demoUserEnabled)
        {
            if (demoUser is null)
            {
                demoUser = new User { Username = "demo", AuthSource = "DB" };
                context.Users.Add(demoUser);
            }
            demoUser.Active = true;
            demoUser.AuthSource = "DB";
            demoUser.Password = BCrypt.Net.BCrypt.HashPassword("demo123");
            demoUser.Roles.Clear();
            demoUser.Roles.Add(operatorRole);
            await context.SaveChangesAsync();
        }
        else if (demoUser is not null && demoUser.Active)
        {
            demoUser.Active = false;
            await context.SaveChangesAsync();
        }

        var hasActiveAdminUser = await context.Users
            .AnyAsync(user => user.Active && user.Roles.Any(role => role.Name == "Admin"));

        if (adminPasswordReset)
        {
            var configuredAdminPassword = GetRequiredAdminPassword(configuration, isDevelopment);
            await CreateOrRestoreAdminAsync(context, adminRole, configuredAdminPassword);
            await context.SaveChangesAsync();
            logger?.LogWarning(
                "WARNING: ADMIN_PASSWORD_RESET flag is ACTIVE! Admin password has been reset. " +
                "Please remove this flag from your environment variables immediately.");
        }
        else if (!hasActiveAdminUser)
        {
            var configuredAdminPassword = GetRequiredAdminPassword(configuration, isDevelopment);
            var restoredExistingAdmin = await CreateOrRestoreAdminAsync(
                context, adminRole, configuredAdminPassword);
            await context.SaveChangesAsync();
            if (restoredExistingAdmin)
            {
                logger?.LogWarning(
                    "WARNING: The existing admin account was restored and its password was reset from " +
                    "ADMIN_PASSWORD. Active refresh tokens for the admin account were revoked.");
            }
        }

        if (demoDataEnabled)
            await DemoDataSeeder.SeedAsync(context);
    }

    private static async Task<bool> CreateOrRestoreAdminAsync(
        MtsDbContext context,
        Role adminRole,
        string configuredPassword)
    {
        var admin = await context.Users.Include(user => user.Roles)
            .FirstOrDefaultAsync(user => user.Username == "admin");

        if (admin is null)
        {
            admin = new User
            {
                Username = "admin",
                Password = BCrypt.Net.BCrypt.HashPassword(configuredPassword),
                AuthSource = "DB",
                Active = true,
                Roles = [adminRole]
            };
            context.Users.Add(admin);
            return false;
        }

        admin.Active = true;
        admin.AuthSource = "DB";
        if (!admin.Roles.Any(role => role.Name == "Admin"))
            admin.Roles.Add(adminRole);
        admin.Password = BCrypt.Net.BCrypt.HashPassword(configuredPassword);

        var activeRefreshTokens = await context.RefreshTokens
            .Where(token => token.UserId == admin.Id && token.RevokedAt == null)
            .ToListAsync();
        foreach (var token in activeRefreshTokens)
            token.RevokedAt = DateTime.UtcNow;

        return true;
    }

    private static string GetRequiredAdminPassword(IConfiguration configuration, bool isDevelopment)
    {
        var configuredPassword = configuration["ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(configuredPassword) && isDevelopment)
            configuredPassword = "admin123";
        if (string.IsNullOrWhiteSpace(configuredPassword))
            throw new InvalidOperationException(
                "No active Admin account exists and ADMIN_PASSWORD is not configured. " +
                "Set ADMIN_PASSWORD to create or restore the administrator account.");
        if (!isDevelopment &&
            (configuredPassword.Length < 12 ||
             configuredPassword.Equals("admin123", StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException(
                "ADMIN_PASSWORD must be at least 12 characters and must not be the default value.");
        }

        return configuredPassword;
    }

    private static bool ReadBooleanSetting(IConfiguration configuration, string key, bool defaultValue)
    {
        var configuredValue = configuration[key];
        if (string.IsNullOrWhiteSpace(configuredValue))
            return defaultValue;
        if (bool.TryParse(configuredValue, out var value))
            return value;
        throw new InvalidOperationException($"{key} must be true or false.");
    }
}
