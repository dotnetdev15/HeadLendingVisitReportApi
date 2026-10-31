using System.Data;
using System.Data.Common;
using HeadLendingVisitReportApi.Data;
using HeadLendingVisitReportApi.Models.Dtos;
using HeadLendingVisitReportApi.Models.Entities;
using Npgsql;
using NpgsqlTypes;
using static HeadLendingVisitReportApi.Data.PgCommand;

namespace HeadLendingVisitReportApi.Repositories;

public class AttachmentRepository : IAttachmentRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;

    public AttachmentRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }
    public async Task<int> InsertAsync(AttachmentRecord record)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = Function(connection, "dbo.usp_Attachment_Insert",
            P("VisitReportId", NpgsqlDbType.Integer, record.VisitReportId),
            P("FileName", NpgsqlDbType.Varchar, record.FileName),
            P("StoredFileName", NpgsqlDbType.Varchar, record.StoredFileName),
            P("FilePath", NpgsqlDbType.Varchar, record.FilePath),
            P("ContentType", NpgsqlDbType.Varchar, record.ContentType),
            P("FileSizeBytes", NpgsqlDbType.Bigint, record.FileSizeBytes));

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    public async Task<List<AttachmentRecord>> GetByVisitReportIdAsync(int visitReportId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = Function(connection, "dbo.usp_Attachment_GetByVisitReportId",
            P("VisitReportId", NpgsqlDbType.Integer, visitReportId));

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        var results = new List<AttachmentRecord>();
        while (await reader.ReadAsync()) results.Add(MapRecord(reader));
        return results;
    }
    public async Task<AttachmentRecord> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = Function(connection, "dbo.usp_Attachment_GetById",
            P("Id", NpgsqlDbType.Integer, id));

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync() ? MapRecord(reader) : null;
    }
    public async Task<int> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = Function(connection, "dbo.usp_Attachment_Delete",
            P("Id", NpgsqlDbType.Integer, id));

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }
    private static AttachmentRecord MapRecord(DbDataReader reader) => new()
    {
        Id = reader.GetInt32Value("Id"),
        VisitReportId = reader.GetInt32Value("VisitReportId"),
        FileName = reader.GetValueOrDefault<string>("FileName"),
        StoredFileName = reader.GetValueOrDefault<string>("StoredFileName"),
        FilePath = reader.GetValueOrDefault<string>("FilePath"),
        ContentType = reader.GetValueOrDefault<string>("ContentType"),
        FileSizeBytes = reader.GetValueOrDefault<long>("FileSizeBytes"),
        UploadedAtUtc = reader.GetValueOrDefault<DateTime>("UploadedAtUtc"),
    };
}
