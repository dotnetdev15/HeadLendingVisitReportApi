using System.Data.Common;
using System.Globalization;
using System.Text.Json;
using Npgsql;
using NpgsqlTypes;

namespace HeadLendingVisitReportApi.Data;

/// <summary>
/// Small convenience helpers for pulling nullable column values out of a
/// data reader without hand-checking DBNull at every call site, plus the
/// helpers used to call PostgreSQL functions the way stored procedures
/// were called before.
/// </summary>
public static class SqlDataReaderExtensions
{
    public static T GetValueOrDefault<T>(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal)) return default;
        return (T)reader.GetValue(ordinal);
    }

    public static int GetInt32Value(this DbDataReader reader, string columnName)
        => Convert.ToInt32(reader.GetValue(reader.GetOrdinal(columnName)));
}

/// <summary>
/// Builds commands that call PostgreSQL functions. PostgreSQL 10 has no
/// CREATE PROCEDURE, so each former stored procedure is a function that is
/// called as <c>SELECT * FROM dbo.fn(@a, @b, ...)</c>. Several calls can be
/// batched in one command so <c>NextResultAsync()</c> walks the result sets
/// in the same order the old multi-result procedures returned them.
/// </summary>
public static class PgCommand
{
    public static NpgsqlParameter P(string name, NpgsqlDbType type, object value)
        => new(name, type) { Value = value ?? DBNull.Value };

    /// <summary>"SELECT * FROM fn(@p1, @p2, ...)" — argument order = parameter order.</summary>
    public static string Call(string functionName, params NpgsqlParameter[] args)
        => $"SELECT * FROM {functionName}({string.Join(", ", args.Select(a => "@" + a.ParameterName.TrimStart('@')))})";

    public static string Call(string functionName, params string[] parameterNames)
        => $"SELECT * FROM {functionName}({string.Join(", ", parameterNames.Select(n => "@" + n.TrimStart('@')))})";

    public static NpgsqlCommand Function(NpgsqlConnection connection, string functionName, params NpgsqlParameter[] args)
    {
        var command = new NpgsqlCommand(Call(functionName, args), connection);
        foreach (var a in args) command.Parameters.Add(a);
        return command;
    }

    /// <summary>
    /// Replacement for a SQL Server Table-Valued Parameter: serialises a DataTable
    /// to a JSON array (one object per row, column names as keys) that the
    /// PostgreSQL function expands with jsonb_to_recordset.
    /// </summary>
    public static NpgsqlParameter Json(string name, System.Data.DataTable table)
    {
        var rows = new List<Dictionary<string, object>>();
        foreach (System.Data.DataRow row in table.Rows)
        {
            var dict = new Dictionary<string, object>();
            foreach (System.Data.DataColumn col in table.Columns)
                dict[col.ColumnName] = row[col] == DBNull.Value ? null : row[col];
            rows.Add(dict);
        }
        return new NpgsqlParameter(name, NpgsqlDbType.Jsonb) { Value = JsonSerializer.Serialize(rows) };
    }

    /// <summary>
    /// The query DTO carries dates as strings; SqlClient used to convert them
    /// implicitly. Npgsql needs a real DateTime, so parse here (blank -> NULL).
    /// </summary>
    public static object ToDbDate(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return DBNull.Value;
        var v = value.Trim();
        if (DateTime.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) return d;
        if (DateTime.TryParse(v, CultureInfo.CurrentCulture, DateTimeStyles.None, out d)) return d;
        throw new FormatException($"'{value}' is not a valid date.");
    }
}
