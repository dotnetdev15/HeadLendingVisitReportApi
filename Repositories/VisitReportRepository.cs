using System.Data;
using HeadLendingVisitReportApi.Data;
using HeadLendingVisitReportApi.Models.Dtos;
using Microsoft.Data.SqlClient;

namespace HeadLendingVisitReportApi.Repositories;

public class VisitReportRepository : IVisitReportRepository
{
    private readonly ISqlConnectionFactory _connectionFactory;
    public VisitReportRepository(ISqlConnectionFactory connectionFactory)
    {
        _connectionFactory = connectionFactory;
    }

    #region
    public async Task<int> CreateAsync(VisitReportDto dto)
    {
        try
        {
            using var connection = _connectionFactory.CreateConnection();
            using var command = new SqlCommand("dbo.usp_VisitReport_Create", connection)
            {
                CommandType = CommandType.StoredProcedure
            };

            AddHeaderParameters(command, dto);
            AddTvpParameters(command, dto);

            await connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();

            return Convert.ToInt32(result);
        }
        catch (SqlException ex)
        {
            throw new Exception($"Failed to create visit report: {ex.Message}", ex);
        }
    }
    public async Task<bool> UpdateAsync(int id, VisitReportDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_Update", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
        AddHeaderParameters(command, dto);
        AddTvpParameters(command, dto);

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result != null;
    }
    public async Task<VisitReportDto> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_GetById", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        if (!await reader.ReadAsync()) return null;
        var dto = MapHeader(reader);

        await reader.NextResultAsync();
        while (await reader.ReadAsync()) dto.BusinessDevelopments.Add(MapBusinessDevelopmentRow(reader));

        await reader.NextResultAsync();
        while (await reader.ReadAsync()) dto.CashManagements.Add(MapCashManagementRow(reader));

        await reader.NextResultAsync();
        while (await reader.ReadAsync()) dto.NpaOdManagements.Add(MapNpaOdManagementRow(reader));

        await reader.NextResultAsync();
        while (await reader.ReadAsync()) dto.NpaBorrowers.Add(MapNpaBorrowerRow(reader));

        await reader.NextResultAsync();
        while (await reader.ReadAsync()) dto.ConcurrentAudits.Add(MapConcurrentAuditRow(reader));

        await reader.NextResultAsync();
        while (await reader.ReadAsync()) dto.Attachments.Add(MapAttachment(reader));

        return dto;
    }
    public async Task<PagedResult<VisitReportDto>> GetAllAsync(VisitReportQuery query)
    {
        query ??= new VisitReportQuery();

        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_GetAll", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        var email = FirstNonEmpty(query.Email, query.ViewerEmail, query.email);

        command.Parameters.Add(new SqlParameter("@Page", SqlDbType.Int) { Value = query.Page < 1 ? 1 : query.Page });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = query.PageSize < 1 ? 20 : query.PageSize });
        command.Parameters.Add(new SqlParameter("@BranchName", SqlDbType.NVarChar, 200) { Value = DbStr(query.BranchName) });
        command.Parameters.Add(new SqlParameter("@EmployeeCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.EmployeeCode) });
        command.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime2) { Value = (object)query.FromDate ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime2) { Value = (object)query.ToDate ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 256) { Value = DbStr(email) });
        command.Parameters.Add(new SqlParameter("@SelectedEmpCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.SelectedEmpCode) });
        command.Parameters.Add(new SqlParameter("@ViewRole", SqlDbType.NVarChar, 50) { Value = DbStr(query.ViewRole) });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        var items = new List<VisitReportDto>();
        while (await reader.ReadAsync()) items.Add(MapHeader(reader));

        var totalCount = 0;
        if (await reader.NextResultAsync() && await reader.ReadAsync())
        {
            totalCount = reader.GetInt32Value("TotalCount");
        }

        return new PagedResult<VisitReportDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize < 1 ? 20 : query.PageSize
        };
    }
    public async Task<PagedResult<VisitReportDto>> GetByRoleTeamAsync(VisitReportQuery query)
    {
        query ??= new VisitReportQuery();

        var email = FirstNonEmpty(query.Email, query.ViewerEmail, query.email);
        var empType = FirstNonEmpty(query.EmpType, query.ViewRole);

        var empCode = FirstNonEmpty(query.EmpCode, query.empCode);

        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_GetByRoleTeam", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };

        command.Parameters.Add(new SqlParameter("@Page", SqlDbType.Int) { Value = query.Page < 1 ? 1 : query.Page });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = query.PageSize < 1 ? 20 : query.PageSize });
        command.Parameters.Add(new SqlParameter("@BranchName", SqlDbType.NVarChar, 200) { Value = DbStr(query.BranchName) });
        command.Parameters.Add(new SqlParameter("@EmployeeCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.EmployeeCode) });
        command.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime2) { Value = (object)query.FromDate ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime2) { Value = (object)query.ToDate ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@Email", SqlDbType.NVarChar, 256) { Value = DbStr(email) });
        command.Parameters.Add(new SqlParameter("@EmpCode", SqlDbType.NVarChar, 50) { Value = DbStr(empCode) });
        command.Parameters.Add(new SqlParameter("@EmpType", SqlDbType.NVarChar, 50) { Value = DbStr(empType) });
        command.Parameters.Add(new SqlParameter("@Search", SqlDbType.NVarChar, 200) { Value = DbStr(query.Search) });
        command.Parameters.Add(new SqlParameter("@RoleName", SqlDbType.NVarChar, 50) { Value = DBNull.Value });

        await connection.OpenAsync();

        using var reader = await command.ExecuteReaderAsync();
        var items = new List<VisitReportDto>();
        while (await reader.ReadAsync())
            items.Add(MapHeader(reader));
        var totalCount = 0;
        if (await reader.NextResultAsync() && await reader.ReadAsync())
            totalCount = reader.GetInt32Value("TotalCount");
        return new PagedResult<VisitReportDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize < 1 ? 20 : query.PageSize
        };
    }
    public async Task<PagedResult<AdminVisitSummaryDto>> GetAdminVisitSummaryAsync(AdminVisitReportQuery query)
    {
        query ??= new AdminVisitReportQuery();

        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_AdminVisitSummary", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };

        command.Parameters.Add(new SqlParameter("@Page", SqlDbType.Int) { Value = query.Page < 1 ? 1 : query.Page });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = query.PageSize < 1 ? 10 : query.PageSize });
        command.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime2) { Value = (object)query.FromDate ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime2) { Value = (object)query.ToDate ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@EmpType", SqlDbType.NVarChar, 50) { Value = DbStr(query.EmpType) });
        command.Parameters.Add(new SqlParameter("@Creator", SqlDbType.NVarChar, 100) { Value = DbStr(query.Creator) });
        command.Parameters.Add(new SqlParameter("@BranchCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.BranchCode) });
        command.Parameters.Add(new SqlParameter("@EmployeeCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.EmployeeCode) });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        var items = new List<AdminVisitSummaryDto>();
        while (await reader.ReadAsync())
        {
            items.Add(new AdminVisitSummaryDto
            {
                Name = TryGetString(reader, "Name"),
                EmpCode = TryGetString(reader, "EmpCode"),
                VisitCount = HasColumn(reader, "VisitCount") &&reader["VisitCount"] != DBNull.Value? Convert.ToInt32(reader["VisitCount"]): 0,
                AssignedBranchCount = HasColumn(reader, "AssignedBranchCount") &&reader["AssignedBranchCount"] != DBNull.Value? Convert.ToInt32(reader["AssignedBranchCount"]): 0,
                RequiredVisitCount = HasColumn(reader, "RequiredVisitCount") &&reader["RequiredVisitCount"] != DBNull.Value? Convert.ToInt32(reader["RequiredVisitCount"]): 0,
                RequirementMet = HasColumn(reader, "RequirementMet") &&reader["RequirementMet"] != DBNull.Value &&Convert.ToBoolean(reader["RequirementMet"])
            });
        }
        var totalCount = 0;
        if (await reader.NextResultAsync() && await reader.ReadAsync())
            totalCount = reader.GetInt32Value("TotalCount");

        return new PagedResult<AdminVisitSummaryDto>
        {
            Items = items,
            TotalCount = totalCount,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize < 1 ? 10 : query.PageSize
        };
    }
    public async Task<List<AdminVisitDetailDto>> GetAdminVisitDetailsAsync(AdminVisitReportQuery query)
    {
        query ??= new AdminVisitReportQuery();
        if (string.IsNullOrWhiteSpace(query.EmployeeCode))
            throw new ArgumentException("EmployeeCode is required.");

        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_AdminVisitDetails", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };

        command.Parameters.Add(new SqlParameter("@FromDate", SqlDbType.DateTime2) { Value = (object)query.FromDate ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ToDate", SqlDbType.DateTime2) { Value = (object)query.ToDate ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@EmployeeCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.EmployeeCode) });
        command.Parameters.Add(new SqlParameter("@EmpType", SqlDbType.NVarChar, 50) { Value = DbStr(query.EmpType) });
        command.Parameters.Add(new SqlParameter("@Creator", SqlDbType.NVarChar, 100) { Value = DbStr(query.Creator) });
        command.Parameters.Add(new SqlParameter("@BranchCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.BranchCode) });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        var items = new List<AdminVisitDetailDto>();
        while (await reader.ReadAsync())
        {
            items.Add(new AdminVisitDetailDto
            {
                VisitReportId = HasColumn(reader, "VisitReportId") && reader["VisitReportId"] != DBNull.Value
                    ? Convert.ToInt32(reader["VisitReportId"])
                    : 0,
                BranchCode = TryGetString(reader, "BranchCode"),
                BranchName = TryGetString(reader, "BranchName"),
                Creator = TryGetString(reader, "Creator"),
                VisitDateTime = reader.GetValueOrDefault<DateTime?>("VisitDateTime"),
                Name = TryGetString(reader, "Name"),
                EmpCode = TryGetString(reader, "EmpCode")
            });
        }

        return items;
    }
    private static string FirstNonEmpty(params string[] values)
    {
        if (values == null) return null;
        foreach (var v in values)
        {
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return null;
    }
    public async Task<int> DeleteAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_Delete", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }
    private static void AddHeaderParameters(SqlCommand command, VisitReportDto dto)
    {
        command.Parameters.Add(new SqlParameter("@BranchName", SqlDbType.NVarChar, 200) { Value = (object)dto.BranchName ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@HeadName", SqlDbType.NVarChar, 200) { Value = (object)dto.HeadName ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@EmployeeCode", SqlDbType.NVarChar, 50) { Value = (object)dto.EmployeeCode ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@VisitDateTime", SqlDbType.DateTime2) { Value = (object)dto.VisitDateTime ?? DBNull.Value });

        command.Parameters.Add(new SqlParameter("@DemandForMonth", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = (object)dto.DemandForMonth ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@CollectionAmount", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = (object)dto.CollectionAmount ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@Shortfall", SqlDbType.Decimal) { Precision = 18, Scale = 2, Value = (object)dto.Shortfall ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@CollectionPercent", SqlDbType.Decimal) { Precision = 5, Scale = 2, Value = (object)dto.CollectionPercent ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@CollectionRemarks", SqlDbType.NVarChar, 500) { Value = (object)dto.CollectionRemarks ?? DBNull.Value });

        command.Parameters.Add(new SqlParameter("@ObsBusiness", SqlDbType.NVarChar, -1) { Value = (object)dto.ObsBusiness ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ObsCash", SqlDbType.NVarChar, -1) { Value = (object)dto.ObsCash ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ObsOd", SqlDbType.NVarChar, -1) { Value = (object)dto.ObsOd ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ObsNpa", SqlDbType.NVarChar, -1) { Value = (object)dto.ObsNpa ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ObsCollection", SqlDbType.NVarChar, -1) { Value = (object)dto.ObsCollection ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ObsAudit", SqlDbType.NVarChar, -1) { Value = (object)dto.ObsAudit ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@ObsOthers", SqlDbType.NVarChar, -1) { Value = (object)dto.ObsOthers ?? DBNull.Value });

        command.Parameters.Add(new SqlParameter("@SignatureName", SqlDbType.NVarChar, 200) { Value = (object)dto.SignatureName ?? DBNull.Value });
        command.Parameters.Add(new SqlParameter("@SignatureDate", SqlDbType.Date) { Value = (object)dto.SignatureDate ?? DBNull.Value });
    }
    private static void AddTvpParameters(SqlCommand command, VisitReportDto dto)
    {
        AddTvp(command, "@BusinessDevelopments", "dbo.BusinessDevelopmentRowType", ToBusinessDevelopmentTable(dto.BusinessDevelopments));
        AddTvp(command, "@CashManagements", "dbo.CashManagementRowType", ToCashManagementTable(dto.CashManagements));
        AddTvp(command, "@NpaOdManagements", "dbo.NpaOdManagementRowType", ToNpaOdManagementTable(dto.NpaOdManagements));
        AddTvp(command, "@NpaBorrowers", "dbo.NpaBorrowerRowType", ToNpaBorrowerTable(dto.NpaBorrowers));
        AddTvp(command, "@ConcurrentAudits", "dbo.ConcurrentAuditRowType", ToConcurrentAuditTable(dto.ConcurrentAudits));
    }
    private static void AddTvp(SqlCommand command, string paramName, string typeName, DataTable table)
    {
        var parameter = command.Parameters.AddWithValue(paramName, table);
        parameter.SqlDbType = SqlDbType.Structured;
        parameter.TypeName = typeName;
    }
    private static DataTable ToBusinessDevelopmentTable(List<BusinessDevelopmentRowDto> rows)
    {
        var table = new DataTable();
        table.Columns.Add("RowOrder", typeof(int));
        table.Columns.Add("TypeOfLoan", typeof(string));
        table.Columns.Add("Budget", typeof(decimal));
        table.Columns.Add("Sanctioned", typeof(decimal));
        table.Columns.Add("Disbursed", typeof(decimal));

        foreach (var row in rows ?? new List<BusinessDevelopmentRowDto>())
        {
            table.Rows.Add(
                row.RowOrder,
                (object)row.TypeOfLoan ?? DBNull.Value,
                (object)row.Budget ?? DBNull.Value,
                (object)row.Sanctioned ?? DBNull.Value,
                (object)row.Disbursed ?? DBNull.Value);
        }
        return table;
    }
    private static DataTable ToCashManagementTable(List<CashManagementRowDto> rows)
    {
        var table = new DataTable();
        table.Columns.Add("RowOrder", typeof(int));
        table.Columns.Add("Particulars", typeof(string));
        table.Columns.Add("Observation", typeof(string));
        table.Columns.Add("Remarks", typeof(string));

        foreach (var row in rows ?? new List<CashManagementRowDto>())
        {
            table.Rows.Add(
                row.RowOrder,
                (object)row.Particulars ?? DBNull.Value,
                (object)row.Observation ?? DBNull.Value,
                (object)row.Remarks ?? DBNull.Value);
        }
        return table;
    }
    private static DataTable ToNpaOdManagementTable(List<NpaOdManagementRowDto> rows)
    {
        var table = new DataTable();
        table.Columns.Add("RowOrder", typeof(int));
        table.Columns.Add("Particulars", typeof(string));
        table.Columns.Add("Amount", typeof(decimal));
        table.Columns.Add("RecoveryMade", typeof(decimal));
        table.Columns.Add("Remarks", typeof(string));

        foreach (var row in rows ?? new List<NpaOdManagementRowDto>())
        {
            table.Rows.Add(
                row.RowOrder,
                (object)row.Particulars ?? DBNull.Value,
                (object)row.Amount ?? DBNull.Value,
                (object)row.RecoveryMade ?? DBNull.Value,
                (object)row.Remarks ?? DBNull.Value);
        }
        return table;
    }
    private static DataTable ToNpaBorrowerTable(List<NpaBorrowerRowDto> rows)
    {
        var table = new DataTable();
        table.Columns.Add("RowOrder", typeof(int));
        table.Columns.Add("BorrowerName", typeof(string));
        table.Columns.Add("AccountNo", typeof(string));
        table.Columns.Add("AmountRecovered", typeof(decimal));
        table.Columns.Add("Remarks", typeof(string));

        foreach (var row in rows ?? new List<NpaBorrowerRowDto>())
        {
            table.Rows.Add(
                row.RowOrder,
                (object)row.BorrowerName ?? DBNull.Value,
                (object)row.AccountNo ?? DBNull.Value,
                (object)row.AmountRecovered ?? DBNull.Value,
                (object)row.Remarks ?? DBNull.Value);
        }
        return table;
    }
    private static DataTable ToConcurrentAuditTable(List<ConcurrentAuditRowDto> rows)
    {
        var table = new DataTable();
        table.Columns.Add("RowOrder", typeof(int));
        table.Columns.Add("AuditorObservation", typeof(string));
        table.Columns.Add("BranchCompliance", typeof(string));
        table.Columns.Add("Remarks", typeof(string));

        foreach (var row in rows ?? new List<ConcurrentAuditRowDto>())
        {
            table.Rows.Add(
                row.RowOrder,
                (object)row.AuditorObservation ?? DBNull.Value,
                (object)row.BranchCompliance ?? DBNull.Value,
                (object)row.Remarks ?? DBNull.Value);
        }
        return table;
    }
    private static VisitReportDto MapHeader(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32Value("Id"),
        BranchName = reader.GetValueOrDefault<string>("BranchName"),
        HeadName = reader.GetValueOrDefault<string>("HeadName"),
        EmployeeCode = reader.GetValueOrDefault<string>("EmployeeCode"),
        VisitDateTime = reader.GetValueOrDefault<DateTime?>("VisitDateTime"),

        DemandForMonth = reader.GetValueOrDefault<decimal?>("DemandForMonth"),
        CollectionAmount = reader.GetValueOrDefault<decimal?>("CollectionAmount"),
        Shortfall = reader.GetValueOrDefault<decimal?>("Shortfall"),
        CollectionPercent = reader.GetValueOrDefault<decimal?>("CollectionPercent"),
        CollectionRemarks = reader.GetValueOrDefault<string>("CollectionRemarks"),

        ObsBusiness = reader.GetValueOrDefault<string>("ObsBusiness"),
        ObsCash = reader.GetValueOrDefault<string>("ObsCash"),
        ObsOd = reader.GetValueOrDefault<string>("ObsOd"),
        ObsNpa = reader.GetValueOrDefault<string>("ObsNpa"),
        ObsCollection = reader.GetValueOrDefault<string>("ObsCollection"),
        ObsAudit = reader.GetValueOrDefault<string>("ObsAudit"),
        ObsOthers = reader.GetValueOrDefault<string>("ObsOthers"),

        // Uploaded observation files (HeadLendingVisitReport page)
        BusinessTypeImg = TryGetString(reader, "BusinessTypeImg"),
        CashTypeImg = TryGetString(reader, "CashTypeImg"),
        OdTypeImg = TryGetString(reader, "OdTypeImg") ?? TryGetString(reader, "ODTypeImg"),
        NpaTypeImg = TryGetString(reader, "NpaTypeImg") ?? TryGetString(reader, "NPATypeImg"),
        CollectionTypeImg = TryGetString(reader, "CollectionTypeImg"),
        AuditTypeImg = TryGetString(reader, "AuditTypeImg"),
        OthersTypeImg = TryGetString(reader, "OthersTypeImg"),

        FormRole = TryGetString(reader, "FormRole"),

        SignatureName = reader.GetValueOrDefault<string>("SignatureName"),
        SignatureDate = reader.GetValueOrDefault<DateTime?>("SignatureDate"),

        CreatedAtUtc = reader.GetValueOrDefault<DateTime?>("CreatedAtUtc"),
        UpdatedAtUtc = reader.GetValueOrDefault<DateTime?>("UpdatedAtUtc"),
    };
    private static string TryGetString(SqlDataReader reader, string columnName)
    {
        try
        {
            var ordinal = reader.GetOrdinal(columnName);
            if (reader.IsDBNull(ordinal)) return null;
            return reader.GetValue(ordinal)?.ToString();
        }
        catch (IndexOutOfRangeException)
        {
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
    private static BusinessDevelopmentRowDto MapBusinessDevelopmentRow(SqlDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        TypeOfLoan = reader.GetValueOrDefault<string>("TypeOfLoan"),
        Budget = reader.GetValueOrDefault<decimal?>("Budget"),
        Sanctioned = reader.GetValueOrDefault<decimal?>("Sanctioned"),
        Disbursed = reader.GetValueOrDefault<decimal?>("Disbursed"),
    };
    private static CashManagementRowDto MapCashManagementRow(SqlDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        Particulars = reader.GetValueOrDefault<string>("Particulars"),
        Observation = reader.GetValueOrDefault<string>("Observation"),
        Remarks = reader.GetValueOrDefault<string>("Remarks"),
    };
    private static NpaOdManagementRowDto MapNpaOdManagementRow(SqlDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        Particulars = reader.GetValueOrDefault<string>("Particulars"),
        Amount = reader.GetValueOrDefault<decimal?>("Amount"),
        RecoveryMade = reader.GetValueOrDefault<decimal?>("RecoveryMade"),
        Remarks = reader.GetValueOrDefault<string>("Remarks"),
    };
    private static NpaBorrowerRowDto MapNpaBorrowerRow(SqlDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        BorrowerName = reader.GetValueOrDefault<string>("BorrowerName"),
        AccountNo = reader.GetValueOrDefault<string>("AccountNo"),
        AmountRecovered = reader.GetValueOrDefault<decimal?>("AmountRecovered"),
        Remarks = reader.GetValueOrDefault<string>("Remarks"),
    };
    private static ConcurrentAuditRowDto MapConcurrentAuditRow(SqlDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        AuditorObservation = reader.GetValueOrDefault<string>("AuditorObservation"),
        BranchCompliance = reader.GetValueOrDefault<string>("BranchCompliance"),
        Remarks = reader.GetValueOrDefault<string>("Remarks"),
    };
    private static AttachmentDto MapAttachment(SqlDataReader reader) => new()
    {
        Id = reader.GetInt32Value("Id"),
        VisitReportId = reader.GetInt32Value("VisitReportId"),
        FileName = reader.GetValueOrDefault<string>("FileName"),
        FilePath = TryGetString(reader, "FilePath"),
        DocumentCategory = TryGetString(reader, "DocumentCategory"),
        ContentType = reader.GetValueOrDefault<string>("ContentType"),
        FileSizeBytes = reader.GetValueOrDefault<long>("FileSizeBytes"),
        UploadedAtUtc = reader.GetValueOrDefault<DateTime>("UploadedAtUtc"),
    };
    public async Task UpdateImagesAsync(int visitReportId, VisitReportDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();

        using var command = new SqlCommand("dbo.usp_VisitReport_UploadDoc", connection)
        {
            CommandType = CommandType.StoredProcedure
        };

        command.Parameters.Add("@VisitReportId", SqlDbType.Int).Value = visitReportId;

        command.Parameters.Add("@BusinessTypeImg", SqlDbType.NVarChar, -1).Value = (object?)dto.BusinessTypeImg ?? DBNull.Value;
        command.Parameters.Add("@CashTypeImg", SqlDbType.NVarChar, -1).Value = (object?)dto.CashTypeImg ?? DBNull.Value;
        command.Parameters.Add("@OdTypeImg", SqlDbType.NVarChar, -1).Value = (object?)dto.OdTypeImg ?? DBNull.Value;
        command.Parameters.Add("@NpaTypeImg", SqlDbType.NVarChar, -1).Value = (object?)dto.NpaTypeImg ?? DBNull.Value;
        command.Parameters.Add("@CollectionTypeImg", SqlDbType.NVarChar, -1).Value = (object?)dto.CollectionTypeImg ?? DBNull.Value;
        command.Parameters.Add("@AuditTypeImg", SqlDbType.NVarChar, -1).Value = (object?)dto.AuditTypeImg ?? DBNull.Value;
        command.Parameters.Add("@OthersTypeImg", SqlDbType.NVarChar, -1).Value = (object?)dto.OthersTypeImg ?? DBNull.Value;
        command.Parameters.Add("@Email", SqlDbType.NVarChar, 100).Value = DBNull.Value;
        command.Parameters.Add("@Mode", SqlDbType.VarChar, 20).Value = "UPDATE";
        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }
    public async Task<LoginProfileDto> GetLoginProfileByEmpCodeAsync(string empCode)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return null;

        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_GetLoginProfile", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@EmpCode", SqlDbType.NVarChar, 50).Value = empCode.Trim();

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var empType = TryGetString(reader, "EmpType") ?? TryGetString(reader, "RoleName");
        var roleName = empType ?? (reader["RoleName"] as string);
        int? roleId = null;
        try
        {
            if (HasColumn(reader, "RoleId") && reader["RoleId"] != DBNull.Value)
                roleId = Convert.ToInt32(reader["RoleId"]);
        }
        catch { /* older SP without RoleId */ }

        var creator = TryGetString(reader, "Creator");
        var branchCode = TryGetString(reader, "BranchCode");
        var branchName = TryGetString(reader, "BranchName");

        var profile = new LoginProfileDto
        {
            Id = reader["Id"] != DBNull.Value ? Convert.ToInt32(reader["Id"]) : 0,
            RoleId = roleId,
            Email = TryGetString(reader, "Email"),
            Name = TryGetString(reader, "Name"),
            RoleName = roleName,
            FormRole = roleName,
            EmpType = empType,
            BranchCode = branchCode,
            BranchName = branchName,
            Creator = creator,
            EmpCode = TryGetString(reader, "EmpCode") ?? empCode.Trim()
        };

        if (await reader.NextResultAsync())
        {
            while (await reader.ReadAsync())
            {
                var item = TryGetString(reader, "BranchName");
                if (!string.IsNullOrWhiteSpace(item) && !profile.Branches.Contains(item))
                    profile.Branches.Add(item.Trim());
            }
        }

        if (string.IsNullOrWhiteSpace(profile.BranchName)
            && !string.IsNullOrWhiteSpace(creator)
            && !string.IsNullOrWhiteSpace(branchCode))
        {
            profile.BranchName = creator.Trim() + "-" + branchCode.Trim();
        }

        if (profile.Branches.Count > 0 && string.IsNullOrWhiteSpace(profile.BranchName))
            profile.BranchName = profile.Branches[0];

        if (profile.Branches.Count == 0 && !string.IsNullOrWhiteSpace(profile.BranchName))
            profile.Branches.Add(profile.BranchName.Trim());

        return profile;
    }
    private static bool HasColumn(SqlDataReader reader, string columnName)
    {
        for (var i = 0; i < reader.FieldCount; i++)
        {
            if (string.Equals(reader.GetName(i), columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
    public async Task<int> AddCommentAsync(int visitReportId, AddVisitReportCommentRequest req)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_AddComment", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@VisitReportId", SqlDbType.Int).Value = visitReportId;
        command.Parameters.Add("@EmployeeCode", SqlDbType.NVarChar, 50).Value = (object)req.EmployeeCode ?? DBNull.Value;
        command.Parameters.Add("@CommentText", SqlDbType.NVarChar, -1).Value = req.CommentText ?? "";
        command.Parameters.Add("@CreatedByEmail", SqlDbType.NVarChar, 200).Value = (object)req.CreatedByEmail ?? DBNull.Value;
        command.Parameters.Add("@CreatedByName", SqlDbType.NVarChar, 200).Value = (object)req.CreatedByName ?? DBNull.Value;

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    public async Task<List<VisitReportCommentDto>> GetCommentsAsync(int visitReportId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_GetComments", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add("@VisitReportId", SqlDbType.Int).Value = visitReportId;

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        var list = new List<VisitReportCommentDto>();
        while (await reader.ReadAsync())
        {
            list.Add(new VisitReportCommentDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                VisitReportId = Convert.ToInt32(reader["VisitReportId"]),
                EmployeeCode = reader["EmployeeCode"] as string,
                CommentText = reader["CommentText"] as string,
                CreatedByEmail = reader["CreatedByEmail"] as string,
                CreatedByName = reader["CreatedByName"] as string,
                CreatedAtUtc = Convert.ToDateTime(reader["CreatedAtUtc"])
            });
        }
        return list;
    }
    #endregion
    public async Task<PagedResult<CreatorMappingDto>> GetCreatorMappingsAsync(MappingListQuery query)
    {
        query ??= new MappingListQuery();
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_CreatorMapping_List", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };
        command.Parameters.Add(new SqlParameter("@Page", SqlDbType.Int) { Value = query.Page < 1 ? 1 : query.Page });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = query.PageSize < 1 ? 10 : query.PageSize });
        command.Parameters.Add(new SqlParameter("@EmpCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.EmpCode) });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        var items = new List<CreatorMappingDto>();
        while (await reader.ReadAsync())
        {
            items.Add(new CreatorMappingDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                EmpCode = ReadStr(reader, "EmpCode"),
                Creator = ReadStr(reader, "Creator"),
                BranchName = ReadStr(reader, "BranchName"),
                BranchCode = ReadStr(reader, "BranchCode"),
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"])
            });
        }

        var total = 0;
        if (await reader.NextResultAsync() && await reader.ReadAsync())
            total = Convert.ToInt32(reader["TotalCount"]);

        return new PagedResult<CreatorMappingDto>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize < 1 ? 10 : query.PageSize
        };
    }
    public async Task<bool> UpdateCreatorMappingStatusAsync(int id, bool isActive)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_CreatorMapping_UpdateStatus", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
        command.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = isActive });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return Convert.ToInt32(reader["RowsAffected"]) > 0;
        return false;
    }
    public async Task<BulkUploadResultDto> BulkUploadCreatorMappingsAsync(CreatorMappingBulkUploadRequest request)
    {
        request ??= new CreatorMappingBulkUploadRequest();
        var table = new DataTable();
        table.Columns.Add("EmpCode", typeof(string));
        table.Columns.Add("Creator", typeof(string));
        table.Columns.Add("BranchName", typeof(string));
        table.Columns.Add("BranchCode", typeof(string));

        foreach (var row in request.Rows ?? new List<CreatorMappingUploadRow>())
        {
            if (string.IsNullOrWhiteSpace(row?.EmpCode)) continue;
            table.Rows.Add(
                row.EmpCode.Trim(),
                row.Creator?.Trim(),
                row.BranchName?.Trim(),
                row.BranchCode?.Trim());
        }
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_CreatorMapping_BulkUpload", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 120
        };
        var tvp = command.Parameters.AddWithValue("@Rows", table);
        tvp.SqlDbType = SqlDbType.Structured;
        tvp.TypeName = "dbo.VisitReportCreatorMappingUploadType";
        command.Parameters.Add(new SqlParameter("@CreatedBy", SqlDbType.NVarChar, 100)
        {
            Value = string.IsNullOrWhiteSpace(request.CreatedBy) ? DBNull.Value : request.CreatedBy.Trim()
        });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            return new BulkUploadResultDto
            {
                InsertedCount = Convert.ToInt32(reader["InsertedCount"]),
                SkippedCount = Convert.ToInt32(reader["SkippedCount"])
            };
        }
        return new BulkUploadResultDto();
    }
    public async Task<PagedResult<ReportingMappingDto>> GetReportingMappingsAsync(MappingListQuery query)
    {
        query ??= new MappingListQuery();
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_ReportingMapping_List", connection)
        {
            CommandType = CommandType.StoredProcedure,
            CommandTimeout = 60
        };
        command.Parameters.Add(new SqlParameter("@Page", SqlDbType.Int) { Value = query.Page < 1 ? 1 : query.Page });
        command.Parameters.Add(new SqlParameter("@PageSize", SqlDbType.Int) { Value = query.PageSize < 1 ? 10 : query.PageSize });
        command.Parameters.Add(new SqlParameter("@EmpCode", SqlDbType.NVarChar, 50) { Value = DbStr(query.EmpCode) });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        var items = new List<ReportingMappingDto>();
        while (await reader.ReadAsync())
        {
            items.Add(new ReportingMappingDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                EmpCode = ReadStr(reader, "EmpCode"),
                ParentEmpCode = ReadStr(reader, "ParentEmpCode"),
                EmpType = ReadStr(reader, "EmpType"),
                IsActive = reader["IsActive"] != DBNull.Value && Convert.ToBoolean(reader["IsActive"])
            });
        }

        var total = 0;
        if (await reader.NextResultAsync() && await reader.ReadAsync())
            total = Convert.ToInt32(reader["TotalCount"]);

        return new PagedResult<ReportingMappingDto>
        {
            Items = items,
            TotalCount = total,
            Page = query.Page < 1 ? 1 : query.Page,
            PageSize = query.PageSize < 1 ? 10 : query.PageSize
        };
    }
    public async Task<bool> UpdateReportingMappingStatusAsync(int id, bool isActive)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new SqlCommand("dbo.usp_VisitReport_ReportingMapping_UpdateStatus", connection)
        {
            CommandType = CommandType.StoredProcedure
        };
        command.Parameters.Add(new SqlParameter("@Id", SqlDbType.Int) { Value = id });
        command.Parameters.Add(new SqlParameter("@IsActive", SqlDbType.Bit) { Value = isActive });

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return Convert.ToInt32(reader["RowsAffected"]) > 0;
        return false;
    }
    private static object DbStr(string value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static string ReadStr(SqlDataReader reader, string col) => reader[col] == DBNull.Value ? null : Convert.ToString(reader[col])?.Trim();
}