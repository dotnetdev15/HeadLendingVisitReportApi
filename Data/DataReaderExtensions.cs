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
        var value = reader.GetValue(ordinal);
        if (value is T typed) return typed;

        if (targetType == typeof(DateTime))
        {
            if (value is DateOnly dateOnly) return (T)(object)dateOnly.ToDateTime(TimeOnly.MinValue);
            if (value is string text) return (T)(object)DateTime.Parse(text, System.Globalization.CultureInfo.InvariantCulture);
            return (T)(object)reader.GetFieldValue<DateTime>(ordinal);
        }

        // e.g. decimal -> long/int when the row came from the jsonb reader
        return (T)Convert.ChangeType(value, targetType, System.Globalization.CultureInfo.InvariantCulture);
    }

    public static int GetInt32Value(this DbDataReader reader, string columnName)
        => Convert.ToInt32(reader.GetValue(reader.GetOrdinal(columnName)));
}
