using Microsoft.Data.SqlClient;

namespace HeadLendingVisitReportApi.Data;

public interface ISqlConnectionFactory
{
    SqlConnection CreateConnection();
}

/// <summary>
/// Hands out plain ADO.NET SqlConnection instances built from the
/// "DefaultConnection" connection string. No ORM involved — every
/// query in this project goes through a stored procedure.
/// </summary>
public class SqlConnectionFactory : ISqlConnectionFactory
{
    private readonly string _connectionString;

    public SqlConnectionFactory(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Connection string 'DefaultConnection' is not configured.");
    }

    public SqlConnection CreateConnection() => new(_connectionString);
}
