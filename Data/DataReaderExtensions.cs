using System.Data.Common;

namespace HeadLendingVisitReportApi.Data;

/// <summary>
/// Convenience helpers for reading nullable column values from a data reader
/// without checking DBNull at every call site.
/// </summary>
public static class DataReaderExtensions
{
    public static T GetValueOrDefault<T>(this DbDataReader reader, string columnName)
    {
        var ordinal = reader.GetOrdinal(columnName);
        if (reader.IsDBNull(ordinal)) return default;

        // Npgsql 10 returns PostgreSQL 'date' as DateOnly by default; the DTOs use DateTime.
        var targetType = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
        if (targetType == typeof(DateTime))
            return (T)(object)reader.GetFieldValue<DateTime>(ordinal);

        return (T)reader.GetValue(ordinal);
    }

    public static int GetInt32Value(this DbDataReader reader, string columnName)
        => Convert.ToInt32(reader.GetValue(reader.GetOrdinal(columnName)));
}
