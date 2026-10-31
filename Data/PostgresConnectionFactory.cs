using HeadLendingVisitReportApi.Helpers;
using Npgsql;

namespace HeadLendingVisitReportApi.Data;

public interface IDbConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

public class PostgresConnectionFactory : IDbConnectionFactory
{
    private const int DefaultPort = 5432;
    private readonly IConfiguration _configuration;
    private readonly string _connectionString;

    public PostgresConnectionFactory(IConfiguration configuration)
    {
        _configuration = configuration;
        _connectionString = GetPostgresConnectionString();
    }

    public NpgsqlConnection CreateConnection() => new(_connectionString);

    public string GetPostgresConnectionString()
    {
        IConfigurationSection credentials = _configuration.GetSection("Credntials");
        string userSalt = GetRequiredSetting("encryptSalts:userSalt");
        string dbSalt = GetRequiredSetting("encryptSalts:dbSalt");

        string username = DecryptRequired(credentials, "Username", userSalt);
        string password = DecryptRequired(credentials, "Password", dbSalt);
        string database = DecryptRequired(credentials, "Database", dbSalt);

        int port = DefaultPort;
        if (int.TryParse(credentials["Port"], out int parsedPort) && parsedPort > 0)
            port = parsedPort;

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = credentials["HostIp"] ?? string.Empty,
            Port = port,
            Database = database,
            Username = username,
            Password = password,
            Timeout = 120,
            CommandTimeout = 120,
            SslMode = SslMode.Prefer,
            Timezone = GetApiTimeZone()
        };

        return builder.ConnectionString;
    }

    private string GetRequiredSetting(string key)
    {
        string value = _configuration[key];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"Missing configuration value '{key}' in appsettings.json.");
        return value;
    }

    private static string DecryptRequired(IConfigurationSection credentials, string settingName, string salt)
    {
        string encryptedValue = credentials[settingName];
        if (string.IsNullOrWhiteSpace(encryptedValue))
            throw new InvalidOperationException($"Missing configuration value 'Credntials:{settingName}' in appsettings.json.");

        string decryptedValue = EncryptionHelper.Decrypt(encryptedValue, salt);
        if (string.IsNullOrWhiteSpace(decryptedValue))
            throw new InvalidOperationException($"Could not decrypt 'Credntials:{settingName}'. Check that the encryptSalts value matches the one used to encrypt it.");
        return decryptedValue;
    }

    /// <summary>Session time zone for PostgreSQL; "Credntials:TimeZone" overrides the default.</summary>
    private string GetApiTimeZone()
        => _configuration["Credntials:TimeZone"] ?? "Asia/Kolkata";
}
