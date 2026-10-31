using Microsoft.Data.SqlClient;

namespace HeadLendingVisitReportApi.Data;

/// <summary>
/// Small convenience helpers for pulling nullable column values out of a
/// SqlDataReader without hand-checking DBNull at every call site.
/// </summary>
public static class SqlDataReaderExtensions
{
    public static T GetValueOrDefault<T>(this SqlDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal)) return default;
        return (T)reader.GetValue(ordinal);
    }

    public static int GetInt32Value(this SqlDataReader reader, string columnName)
        => reader.GetInt32(reader.GetOrdinal(columnName));
}
