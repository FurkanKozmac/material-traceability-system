using backend.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace backend.Data;

public static class DbInitializer
{
    private static bool PasswordMatches(User user, string configuredPassword)
    {
        if (user.Password.StartsWith("$2", StringComparison.Ordinal))
        {
            try { return BCrypt.Net.BCrypt.Verify(configuredPassword, user.Password); }
            catch (BCrypt.Net.SaltParseException) { return false; }
        }
        if (user.Password.StartsWith("AQAAAA", StringComparison.Ordinal))
            return new PasswordHasher<User>().VerifyHashedPassword(user, user.Password, configuredPassword) != PasswordVerificationResult.Failed;
        return user.Password == configuredPassword;
    }

    public static async Task SeedAsync(MtsDbContext context, IConfiguration configuration, bool isDevelopment)
    {
        var demoDataEnabled = ReadBooleanSetting(configuration, "DEMO_DATA_ENABLED", isDevelopment);
        var demoUserEnabled = ReadBooleanSetting(configuration, "DEMO_USER_ENABLED", isDevelopment);

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
            if (!PasswordMatches(demoUser, "demo123"))
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

        var configuredAdminPassword = configuration["ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(configuredAdminPassword) && isDevelopment)
            configuredAdminPassword = "admin123";
        if (string.IsNullOrWhiteSpace(configuredAdminPassword))
            throw new InvalidOperationException("ADMIN_PASSWORD must be configured before seeding the administrator account.");

        var existingAdmin = await context.Users.Include(user => user.Roles)
            .FirstOrDefaultAsync(user => user.Username == "admin");
        if (existingAdmin is null)
        {
            existingAdmin = new User
            {
                Username = "admin",
                Password = BCrypt.Net.BCrypt.HashPassword(configuredAdminPassword),
                AuthSource = "DB",
                Active = true,
                Roles = [adminRole]
            };
            context.Users.Add(existingAdmin);
        }
        else
        {
            if (!PasswordMatches(existingAdmin, configuredAdminPassword))
                existingAdmin.Password = BCrypt.Net.BCrypt.HashPassword(configuredAdminPassword);
            if (!existingAdmin.Roles.Any(role => role.Name == "Admin"))
                existingAdmin.Roles.Add(adminRole);
        }
        await context.SaveChangesAsync();

        if (demoDataEnabled)
            await DemoDataSeeder.SeedAsync(context);
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
