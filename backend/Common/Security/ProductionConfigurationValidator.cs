namespace backend.Common.Security;

public static class ProductionConfigurationValidator
{
    public static void Validate(string? jwtSecret, string? adminPassword, string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(jwtSecret) || jwtSecret.Length < 32)
            throw new InvalidOperationException("Production startup requires JWT_SECRET with at least 32 characters.");
        if (!string.IsNullOrWhiteSpace(adminPassword) &&
            (adminPassword.Length < 12 || adminPassword.Equals("admin123", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException(
                "Production startup requires ADMIN_PASSWORD with at least 12 non-default characters when configured.");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Production startup requires ConnectionStrings__DefaultConnection.");

        try
        {
            var parsedConnection = new Npgsql.NpgsqlConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(parsedConnection.Host) || string.IsNullOrWhiteSpace(parsedConnection.Database) ||
                string.IsNullOrWhiteSpace(parsedConnection.Username) || string.IsNullOrWhiteSpace(parsedConnection.Password) || parsedConnection.Password.Length < 16 ||
                parsedConnection.Password.Equals("postgres", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("ConnectionStrings__DefaultConnection must include host, database, user, and a non-default password of at least 16 characters.");
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException("ConnectionStrings__DefaultConnection is not a valid PostgreSQL connection string.", ex);
        }
    }
}
