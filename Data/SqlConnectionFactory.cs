using Npgsql;

namespace HeadLendingVisitReportApi.Data;

public interface ISqlConnectionFactory
{
    NpgsqlConnection CreateConnection();
}

/// <summary>
/// Hands out plain ADO.NET NpgsqlConnection instances built from the
/// "DefaultConnection" connection string in appsettings.json. No ORM
/// involved — every query in this project goes through a PostgreSQL
/// function (the former SQL Server stored procedures).
/// </summary>
public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public NpgsqlConnection CreateConnection() => new(_connectionString);
}
