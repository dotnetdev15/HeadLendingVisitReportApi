using System.Data;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace HeadLendingVisitReportApi.Data;

/// <summary>
/// Builds Npgsql commands that call PostgreSQL functions (the former SQL Server
/// stored procedures). PostgreSQL 10 has no CREATE PROCEDURE, so a call is
/// <c>SELECT * FROM dbo.function_name(@param1, @param2, ...)</c>. Several calls
/// can be joined into one command so NextResultAsync() walks the result sets in
/// the same order the old multi-result procedures returned them.
/// </summary>
public static class PostgresCommandBuilder
{
    /// <summary>Creates a typed parameter; a null value is sent as DBNull.</summary>
    public static NpgsqlParameter CreateParameter(string parameterName, NpgsqlDbType dbType, object value)
        => new(parameterName, dbType) { Value = value ?? DBNull.Value };

    /// <summary>Returns "SELECT * FROM function(@p1, @p2, ...)"; argument order = parameter order.</summary>
    public static string BuildFunctionCall(string functionName, params NpgsqlParameter[] parameters)
        => BuildFunctionCall(functionName, parameters.Select(parameter => parameter.ParameterName).ToArray());

    /// <summary>Returns "SELECT * FROM function(@name1, @name2, ...)".</summary>
    public static string BuildFunctionCall(string functionName, params string[] parameterNames)
        => $"SELECT * FROM {functionName}({string.Join(", ", parameterNames.Select(name => "@" + name.TrimStart('@')))})";

    /// <summary>Creates a command that calls one function with the given parameters.</summary>
    public static NpgsqlCommand CreateFunctionCommand(NpgsqlConnection connection, string functionName, params NpgsqlParameter[] parameters)
    {
        var command = new NpgsqlCommand(BuildFunctionCall(functionName, parameters), connection);
        command.Parameters.AddRange(parameters);
        return command;
    }

    /// <summary>
    /// Replacement for a SQL Server Table-Valued Parameter: serialises the DataTable
    /// to a JSON array (one object per row, column names as keys) that the
    /// PostgreSQL function expands with jsonb_to_recordset.
    /// </summary>
    public static NpgsqlParameter CreateJsonParameter(string parameterName, DataTable table)
    {
        var rows = new List<Dictionary<string, object>>();
        foreach (DataRow row in table.Rows)
        {
            var values = new Dictionary<string, object>();
            foreach (DataColumn column in table.Columns)
                values[column.ColumnName] = row[column] == DBNull.Value ? null : row[column];
            rows.Add(values);
        }
        return new NpgsqlParameter(parameterName, NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(rows) };
    }

    /// <summary>
    /// Parses a date sent as a string in the query DTO (SqlClient used to convert
    /// it implicitly). Blank -> DBNull; an invalid date throws FormatException.
    /// </summary>
    public static object ParseDbDate(string dateText)
    {
        if (string.IsNullOrWhiteSpace(dateText)) return DBNull.Value;
        var trimmed = dateText.Trim();
        if (DateTime.TryParse(trimmed, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDate)) return parsedDate;
        if (DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.None, out parsedDate)) return parsedDate;
        throw new FormatException($"'{dateText}' is not a valid date.");
    }
}
