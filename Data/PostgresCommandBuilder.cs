using System.Data;
using System.Data.Common;
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

    /// <summary>
    /// Returns "SELECT * FROM function(p_name1 => @Name1, p_name2 => @Name2, ...)": named arguments,
    /// matched to the function's p_&lt;lowercase name&gt; parameters regardless of their order.
    /// </summary>
    public static string BuildNamedFunctionCall(string functionName, params string[] parameterNames)
        => $"SELECT * FROM {functionName}({string.Join(", ", parameterNames.Select(name => $"p_{name.TrimStart('@').ToLowerInvariant()} => @{name.TrimStart('@')}"))})";

    /// <summary>Creates a command that calls one function with the given parameters.</summary>
    public static NpgsqlCommand CreateFunctionCommand(NpgsqlConnection connection, string functionName, params NpgsqlParameter[] parameters)
    {
        var command = new NpgsqlCommand(BuildFunctionCall(functionName, parameters), connection);
        command.Parameters.AddRange(parameters);
        return command;
    }

    /// <summary>
    /// Runs a function that returns refcursors (PostgreSQL's equivalent of a SQL Server
    /// stored procedure returning several result sets) and returns ONE reader over all of
    /// them, in order, so the caller reads it with ReadAsync()/NextResultAsync() exactly as
    /// it read the old SqlDataReader. The connection must already be open; cursors only
    /// live inside a transaction, which is rolled back when the connection is disposed.
    /// </summary>
    public static async Task<NpgsqlDataReader> ExecuteCursorReaderAsync(this NpgsqlCommand command)
    {
        var connection = command.Connection;
        var transaction = await connection.BeginTransactionAsync();
        command.Transaction = transaction;

        var cursorNames = new List<string>();
        bool returnsCursors;
        using (var cursorReader = await command.ExecuteReaderAsync())
        {
            returnsCursors = cursorReader.FieldCount == 1
                && string.Equals(cursorReader.GetDataTypeName(0), "refcursor", StringComparison.OrdinalIgnoreCase);
            if (returnsCursors)
            {
                while (await cursorReader.ReadAsync())
                    if (!cursorReader.IsDBNull(0)) cursorNames.Add(cursorReader.GetString(0));
            }
        }

        // The function in the database returns rows directly (not refcursors): read them as-is.
        if (!returnsCursors)
            return await command.ExecuteReaderAsync();

        if (cursorNames.Count == 0)
            throw new InvalidOperationException($"Function '{command.CommandText}' returned no cursors.");

        var fetchCommand = new NpgsqlCommand(
            string.Join("; ", cursorNames.Select(cursorName => $"FETCH ALL IN \"{cursorName.Replace("\"", "\"\"")}\"")),
            connection, transaction)
        {
            CommandTimeout = command.CommandTimeout
        };
        return await fetchCommand.ExecuteReaderAsync();
    }

    /// <summary>
    /// For a function that returns SETOF jsonb with several sections appended one after
    /// another (RETURN QUERY ...; RETURN QUERY ...;). Splits the jsonb rows back into
    /// separate result sets so the caller reads them with ReadAsync()/NextResultAsync()
    /// exactly like the old SqlDataReader. sectionKeys = one JSON key that only that
    /// section has, in result-set order. A section with no rows is still returned as an
    /// empty result set, so the order never shifts.
    /// </summary>
    public static async Task<DbDataReader> ExecuteJsonSetReaderAsync(this NpgsqlCommand command, params string[] sectionKeys)
    {
        // No keys = the function returns one section: every jsonb row goes into one result set.
        var singleSection = sectionKeys == null || sectionKeys.Length == 0;
        var tables = singleSection ? new[] { new DataTable() } : sectionKeys.Select(_ => new DataTable()).ToArray();

        using (var reader = await command.ExecuteReaderAsync())
        {
            // The function returns normal columns (not one jsonb column): load them as-is.
            if (!(reader.FieldCount == 1 && string.Equals(reader.GetDataTypeName(0), "jsonb", StringComparison.OrdinalIgnoreCase)))
            {
                tables[0].Load(reader);
                return new DataTableReader(tables);
            }

            while (await reader.ReadAsync())
            {
                if (reader.IsDBNull(0)) continue;
                using var document = JsonDocument.Parse(reader.GetString(0));
                var row = document.RootElement;

                var index = singleSection ? 0 : Array.FindIndex(sectionKeys, key => row.TryGetProperty(key, out _));
                if (index < 0) continue;
                var table = tables[index];

                foreach (var property in row.EnumerateObject())
                    if (!table.Columns.Contains(property.Name))
                        table.Columns.Add(property.Name, typeof(object));

                var dataRow = table.NewRow();
                foreach (var property in row.EnumerateObject())
                    dataRow[property.Name] = JsonToDbValue(property.Name, property.Value);
                table.Rows.Add(dataRow);
            }
        }
        return new DataTableReader(tables);
    }

    /// <summary>Converts a jsonb value to the CLR type the column had before to_jsonb().</summary>
    private static object JsonToDbValue(string name, JsonElement value)
    {
        switch (value.ValueKind)
        {
            case JsonValueKind.Null:
            case JsonValueKind.Undefined: return DBNull.Value;
            case JsonValueKind.True: return true;
            case JsonValueKind.False: return false;
            case JsonValueKind.Number:
                if ((name.EndsWith("id", StringComparison.OrdinalIgnoreCase) || name.Equals("roworder", StringComparison.OrdinalIgnoreCase))
                    && value.TryGetInt32(out var intValue)) return intValue;
                return value.GetDecimal();
            case JsonValueKind.String:
                var text = value.GetString();
                if ((name.Contains("date", StringComparison.OrdinalIgnoreCase) || name.EndsWith("utc", StringComparison.OrdinalIgnoreCase))
                    && DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue)) return dateValue;
                return text;
            default: return value.GetRawText();
        }
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