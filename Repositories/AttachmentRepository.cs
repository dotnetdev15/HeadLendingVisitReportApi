using System.Data;
using HeadLendingVisitReportApi.Data;
using HeadLendingVisitReportApi.Models.Dtos;
using HeadLendingVisitReportApi.Models.Entities;
using Microsoft.Data.SqlClient;

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
        using var command = new SqlCommand("dbo.usp_Attachment_Insert", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(new SqlParameter("@VisitReportId", SqlDbType.Int) { Value = record.VisitReportId });
        command.Parameters.Add(new SqlParameter("@FileName", SqlDbType.NVarChar, 260) { Value = record.FileName });
        command.Parameters.Add(new SqlParameter("@StoredFileName", SqlDbType.NVarChar, 260) { Value = record.StoredFileName });
        command.Parameters.Add(new SqlParameter("@FilePath", SqlDbType.NVarChar, 500) { Value = record.FilePath });
        command.Parameters.Add(new SqlParameter("@ContentType", SqlDbType.NVarChar, 100) { Value = (object)record.ContentType ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@FileSizeBytes", SqlDbType.BigInt) { Value = record.FileSizeBytes });

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    public async Task<List<AttachmentRecord>> GetByVisitReportIdAsync(int visitReportId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_Attachment_GetByVisitReportId", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@VisitReportId", SqlDbType.Int) { Value = visitReportId });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        var results = new List<AttachmentRecord>();
        while (await reader.ReadAsync()) results.Add(MapRecord(reader));
        return results;
    }
    public async Task<AttachmentRecord> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_Attachment_GetById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        return await reader.ReadAsync() ? MapRecord(reader) : null;
    }
    public async Task<int> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_Attachment_Delete", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }
    private static AttachmentRecord MapRecord(SqlDataReader reader) => new()
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