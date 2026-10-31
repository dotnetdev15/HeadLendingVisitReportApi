using System.Data;
using System.Data.Common;
using HeadLendingVisitReportApi.Data;
using HeadLendingVisitReportApi.Models.Dtos;
using Npgsql;
using NpgsqlTypes;
using static HeadLendingVisitReportApi.Data.PgCommand;

namespace HeadLendingVisitReportApi.Repositories;

/// <summary>
/// PostgreSQL (Npgsql) version. Every former SQL Server stored procedure is a
/// PostgreSQL function of the same name (see Scripts/PostgreSQL). Procedures
/// that returned several result sets are now several functions sent in one
/// batched command, so the NextResultAsync() reading logic is unchanged.
/// Table-Valued Parameters are sent as JSONB arrays built from the same
/// DataTables as before.
/// </summary>
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
            using var command = new NpgsqlCommand { Connection = connection };

            AddHeaderParameters(command, dto);
            AddTvpParameters(command, dto);
            command.CommandText = Call("dbo.usp_VisitReport_Create", command.Parameters.Select(p => p.ParameterName).ToArray());

            await connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();

            return Convert.ToInt32(result);
        }
        catch (NpgsqlException ex)
        {
            throw new Exception($"Failed to create visit report: {ex.Message}", ex);
        }
    }
    public async Task<bool> UpdateAsync(int id, VisitReportDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new NpgsqlCommand { Connection = connection };

        command.Parameters.Add(P("Id", NpgsqlDbType.Integer, id));
        AddHeaderParameters(command, dto);
        AddTvpParameters(command, dto);
        command.CommandText = Call("dbo.usp_VisitReport_Update", command.Parameters.Select(p => p.ParameterName).ToArray());

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result != null;
    }
    public async Task<VisitReportDto> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        // 7 result sets, same order as the old usp_VisitReport_GetById
        using var command = new NpgsqlCommand(string.Join("; ",
            Call("dbo.usp_VisitReport_GetById", "Id"),
            Call("dbo.usp_VisitReport_GetBusinessDevelopments", "Id"),
            Call("dbo.usp_VisitReport_GetCashManagements", "Id"),
            Call("dbo.usp_VisitReport_GetNpaOdManagements", "Id"),
            Call("dbo.usp_VisitReport_GetNpaBorrowers", "Id"),
            Call("dbo.usp_VisitReport_GetConcurrentAudits", "Id"),
            Call("dbo.usp_Attachment_GetByVisitReportId", "Id")), connection);
        command.Parameters.Add(P("Id", NpgsqlDbType.Integer, id));

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

        var email = FirstNonEmpty(query.Email, query.ViewerEmail, query.email);

        var filterNames = new[] { "BranchName", "EmployeeCode", "FromDate", "ToDate", "Email", "SelectedEmpCode", "ViewRole" };
        using var command = new NpgsqlCommand(string.Join("; ",
            Call("dbo.usp_VisitReport_GetAll", new[] { "Page", "PageSize" }.Concat(filterNames).ToArray()),
            Call("dbo.usp_VisitReport_GetAll_Count", filterNames)), connection);

        command.Parameters.Add(P("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(P("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 20 : query.PageSize));
        command.Parameters.Add(P("BranchName", NpgsqlDbType.Varchar, DbStr(query.BranchName)));
        command.Parameters.Add(P("EmployeeCode", NpgsqlDbType.Varchar, DbStr(query.EmployeeCode)));
        command.Parameters.Add(P("FromDate", NpgsqlDbType.Timestamp, ToDbDate(query.FromDate)));
        command.Parameters.Add(P("ToDate", NpgsqlDbType.Timestamp, ToDbDate(query.ToDate)));
        command.Parameters.Add(P("Email", NpgsqlDbType.Varchar, DbStr(email)));
        command.Parameters.Add(P("SelectedEmpCode", NpgsqlDbType.Varchar, DbStr(query.SelectedEmpCode)));
        command.Parameters.Add(P("ViewRole", NpgsqlDbType.Varchar, DbStr(query.ViewRole)));

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
        var filterNames = new[] { "BranchName", "EmployeeCode", "FromDate", "ToDate", "Email", "EmpCode", "EmpType", "Search", "RoleName" };
        using var command = new NpgsqlCommand(string.Join("; ",
            Call("dbo.usp_VisitReport_GetByRoleTeam", new[] { "Page", "PageSize" }.Concat(filterNames).ToArray()),
            Call("dbo.usp_VisitReport_GetByRoleTeam_Count", filterNames)), connection)
        {
            CommandTimeout = 60
        };

        command.Parameters.Add(P("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(P("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 20 : query.PageSize));
        command.Parameters.Add(P("BranchName", NpgsqlDbType.Varchar, DbStr(query.BranchName)));
        command.Parameters.Add(P("EmployeeCode", NpgsqlDbType.Varchar, DbStr(query.EmployeeCode)));
        command.Parameters.Add(P("FromDate", NpgsqlDbType.Timestamp, ToDbDate(query.FromDate)));
        command.Parameters.Add(P("ToDate", NpgsqlDbType.Timestamp, ToDbDate(query.ToDate)));
        command.Parameters.Add(P("Email", NpgsqlDbType.Varchar, DbStr(email)));
        command.Parameters.Add(P("EmpCode", NpgsqlDbType.Varchar, DbStr(empCode)));
        command.Parameters.Add(P("EmpType", NpgsqlDbType.Varchar, DbStr(empType)));
        command.Parameters.Add(P("Search", NpgsqlDbType.Varchar, DbStr(query.Search)));
        command.Parameters.Add(P("RoleName", NpgsqlDbType.Varchar, DBNull.Value));

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
        var filterNames = new[] { "FromDate", "ToDate", "EmpType", "Creator", "BranchCode", "EmployeeCode" };
        using var command = new NpgsqlCommand(string.Join("; ",
            Call("dbo.usp_VisitReport_AdminVisitSummary", new[] { "Page", "PageSize" }.Concat(filterNames).ToArray()),
            Call("dbo.usp_VisitReport_AdminVisitSummary_Count", filterNames)), connection)
        {
            CommandTimeout = 60
        };

        command.Parameters.Add(P("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(P("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 10 : query.PageSize));
        command.Parameters.Add(P("FromDate", NpgsqlDbType.Timestamp, query.FromDate));
        command.Parameters.Add(P("ToDate", NpgsqlDbType.Timestamp, query.ToDate));
        command.Parameters.Add(P("EmpType", NpgsqlDbType.Varchar, DbStr(query.EmpType)));
        command.Parameters.Add(P("Creator", NpgsqlDbType.Varchar, DbStr(query.Creator)));
        command.Parameters.Add(P("BranchCode", NpgsqlDbType.Varchar, DbStr(query.BranchCode)));
        command.Parameters.Add(P("EmployeeCode", NpgsqlDbType.Varchar, DbStr(query.EmployeeCode)));

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
        using var command = Function(connection, "dbo.usp_VisitReport_AdminVisitDetails",
            P("FromDate", NpgsqlDbType.Timestamp, query.FromDate),
            P("ToDate", NpgsqlDbType.Timestamp, query.ToDate),
            P("EmployeeCode", NpgsqlDbType.Varchar, DbStr(query.EmployeeCode)),
            P("EmpType", NpgsqlDbType.Varchar, DbStr(query.EmpType)),
            P("Creator", NpgsqlDbType.Varchar, DbStr(query.Creator)),
            P("BranchCode", NpgsqlDbType.Varchar, DbStr(query.BranchCode)));
        command.CommandTimeout = 60;

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
        using var command = Function(connection, "dbo.usp_VisitReport_Delete",
            P("Id", NpgsqlDbType.Integer, id));

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }
    private static void AddHeaderParameters(NpgsqlCommand command, VisitReportDto dto)
    {
        command.Parameters.Add(P("BranchName", NpgsqlDbType.Varchar, dto.BranchName));
        command.Parameters.Add(P("HeadName", NpgsqlDbType.Varchar, dto.HeadName));
        command.Parameters.Add(P("EmployeeCode", NpgsqlDbType.Varchar, dto.EmployeeCode));
        command.Parameters.Add(P("VisitDateTime", NpgsqlDbType.Timestamp, dto.VisitDateTime));

        command.Parameters.Add(P("DemandForMonth", NpgsqlDbType.Numeric, dto.DemandForMonth));
        command.Parameters.Add(P("CollectionAmount", NpgsqlDbType.Numeric, dto.CollectionAmount));
        command.Parameters.Add(P("Shortfall", NpgsqlDbType.Numeric, dto.Shortfall));
        command.Parameters.Add(P("CollectionPercent", NpgsqlDbType.Numeric, dto.CollectionPercent));
        command.Parameters.Add(P("CollectionRemarks", NpgsqlDbType.Varchar, dto.CollectionRemarks));

        command.Parameters.Add(P("ObsBusiness", NpgsqlDbType.Text, dto.ObsBusiness));
        command.Parameters.Add(P("ObsCash", NpgsqlDbType.Text, dto.ObsCash));
        command.Parameters.Add(P("ObsOd", NpgsqlDbType.Text, dto.ObsOd));
        command.Parameters.Add(P("ObsNpa", NpgsqlDbType.Text, dto.ObsNpa));
        command.Parameters.Add(P("ObsCollection", NpgsqlDbType.Text, dto.ObsCollection));
        command.Parameters.Add(P("ObsAudit", NpgsqlDbType.Text, dto.ObsAudit));
        command.Parameters.Add(P("ObsOthers", NpgsqlDbType.Text, dto.ObsOthers));

        command.Parameters.Add(P("SignatureName", NpgsqlDbType.Varchar, dto.SignatureName));
        command.Parameters.Add(P("SignatureDate", NpgsqlDbType.Date, dto.SignatureDate?.Date));
    }
    private static void AddTvpParameters(NpgsqlCommand command, VisitReportDto dto)
    {
        AddTvp(command, "BusinessDevelopments", "dbo.BusinessDevelopmentRowType", ToBusinessDevelopmentTable(dto.BusinessDevelopments));
        AddTvp(command, "CashManagements", "dbo.CashManagementRowType", ToCashManagementTable(dto.CashManagements));
        AddTvp(command, "NpaOdManagements", "dbo.NpaOdManagementRowType", ToNpaOdManagementTable(dto.NpaOdManagements));
        AddTvp(command, "NpaBorrowers", "dbo.NpaBorrowerRowType", ToNpaBorrowerTable(dto.NpaBorrowers));
        AddTvp(command, "ConcurrentAudits", "dbo.ConcurrentAuditRowType", ToConcurrentAuditTable(dto.ConcurrentAudits));
    }
    /// <summary>
    /// PostgreSQL has no Table-Valued Parameters: the row set is sent as a JSONB
    /// array and expanded with jsonb_to_recordset inside the function.
    /// typeName is kept only to document which old SQL Server table type it replaces.
    /// </summary>
    private static void AddTvp(NpgsqlCommand command, string paramName, string typeName, DataTable table)
    {
        _ = typeName;
        command.Parameters.Add(Json(paramName, table));
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
    private static VisitReportDto MapHeader(DbDataReader reader) => new()
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
    private static string TryGetString(DbDataReader reader, string columnName)
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
    private static BusinessDevelopmentRowDto MapBusinessDevelopmentRow(DbDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        TypeOfLoan = reader.GetValueOrDefault<string>("TypeOfLoan"),
        Budget = reader.GetValueOrDefault<decimal?>("Budget"),
        Sanctioned = reader.GetValueOrDefault<decimal?>("Sanctioned"),
        Disbursed = reader.GetValueOrDefault<decimal?>("Disbursed"),
    };
    private static CashManagementRowDto MapCashManagementRow(DbDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        Particulars = reader.GetValueOrDefault<string>("Particulars"),
        Observation = reader.GetValueOrDefault<string>("Observation"),
        Remarks = reader.GetValueOrDefault<string>("Remarks"),
    };
    private static NpaOdManagementRowDto MapNpaOdManagementRow(DbDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        Particulars = reader.GetValueOrDefault<string>("Particulars"),
        Amount = reader.GetValueOrDefault<decimal?>("Amount"),
        RecoveryMade = reader.GetValueOrDefault<decimal?>("RecoveryMade"),
        Remarks = reader.GetValueOrDefault<string>("Remarks"),
    };
    private static NpaBorrowerRowDto MapNpaBorrowerRow(DbDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        BorrowerName = reader.GetValueOrDefault<string>("BorrowerName"),
        AccountNo = reader.GetValueOrDefault<string>("AccountNo"),
        AmountRecovered = reader.GetValueOrDefault<decimal?>("AmountRecovered"),
        Remarks = reader.GetValueOrDefault<string>("Remarks"),
    };
    private static ConcurrentAuditRowDto MapConcurrentAuditRow(DbDataReader reader) => new()
    {
        RowOrder = reader.GetInt32Value("RowOrder"),
        AuditorObservation = reader.GetValueOrDefault<string>("AuditorObservation"),
        BranchCompliance = reader.GetValueOrDefault<string>("BranchCompliance"),
        Remarks = reader.GetValueOrDefault<string>("Remarks"),
    };
    private static AttachmentDto MapAttachment(DbDataReader reader) => new()
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

        using var command = Function(connection, "dbo.usp_VisitReport_UploadDoc",
            P("VisitReportId", NpgsqlDbType.Integer, visitReportId),
            P("BusinessTypeImg", NpgsqlDbType.Text, dto.BusinessTypeImg),
            P("CashTypeImg", NpgsqlDbType.Text, dto.CashTypeImg),
            P("OdTypeImg", NpgsqlDbType.Text, dto.OdTypeImg),
            P("NpaTypeImg", NpgsqlDbType.Text, dto.NpaTypeImg),
            P("CollectionTypeImg", NpgsqlDbType.Text, dto.CollectionTypeImg),
            P("AuditTypeImg", NpgsqlDbType.Text, dto.AuditTypeImg),
            P("OthersTypeImg", NpgsqlDbType.Text, dto.OthersTypeImg),
            P("Email", NpgsqlDbType.Varchar, DBNull.Value),
            P("Mode", NpgsqlDbType.Varchar, "UPDATE"));
        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }
    public async Task<LoginProfileDto> GetLoginProfileByEmpCodeAsync(string empCode)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return null;

        using var connection = _connectionFactory.CreateConnection();
        // Result 1 = profile row, result 2 = branch list (was 2 result sets of one SP)
        using var command = new NpgsqlCommand(string.Join("; ",
            Call("dbo.usp_VisitReport_GetLoginProfile", "EmpCode"),
            Call("dbo.usp_VisitReport_GetLoginBranches", "EmpCode")), connection);
        command.Parameters.Add(P("EmpCode", NpgsqlDbType.Varchar, empCode.Trim()));

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
    private static bool HasColumn(DbDataReader reader, string columnName)
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
        using var command = Function(connection, "dbo.usp_VisitReport_AddComment",
            P("VisitReportId", NpgsqlDbType.Integer, visitReportId),
            P("EmployeeCode", NpgsqlDbType.Varchar, req.EmployeeCode),
            P("CommentText", NpgsqlDbType.Text, req.CommentText ?? ""),
            P("CreatedByEmail", NpgsqlDbType.Varchar, req.CreatedByEmail),
            P("CreatedByName", NpgsqlDbType.Varchar, req.CreatedByName));

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    public async Task<List<VisitReportCommentDto>> GetCommentsAsync(int visitReportId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = Function(connection, "dbo.usp_VisitReport_GetComments",
            P("VisitReportId", NpgsqlDbType.Integer, visitReportId));

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
        using var command = new NpgsqlCommand(string.Join("; ",
            Call("dbo.usp_VisitReport_CreatorMapping_List", "Page", "PageSize", "EmpCode"),
            Call("dbo.usp_VisitReport_CreatorMapping_Count", "EmpCode")), connection)
        {
            CommandTimeout = 60
        };
        command.Parameters.Add(P("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(P("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 10 : query.PageSize));
        command.Parameters.Add(P("EmpCode", NpgsqlDbType.Varchar, DbStr(query.EmpCode)));

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
        using var command = Function(connection, "dbo.usp_VisitReport_CreatorMapping_UpdateStatus",
            P("Id", NpgsqlDbType.Integer, id),
            P("IsActive", NpgsqlDbType.Boolean, isActive));

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
        // Rows go as JSONB (replaces TVP dbo.VisitReportCreatorMappingUploadType)
        using var command = Function(connection, "dbo.usp_VisitReport_CreatorMapping_BulkUpload",
            Json("Rows", table),
            P("CreatedBy", NpgsqlDbType.Varchar,
                string.IsNullOrWhiteSpace(request.CreatedBy) ? DBNull.Value : request.CreatedBy.Trim()));
        command.CommandTimeout = 120;

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
        using var command = new NpgsqlCommand(string.Join("; ",
            Call("dbo.usp_VisitReport_ReportingMapping_List", "Page", "PageSize", "EmpCode"),
            Call("dbo.usp_VisitReport_ReportingMapping_Count", "EmpCode")), connection)
        {
            CommandTimeout = 60
        };
        command.Parameters.Add(P("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(P("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 10 : query.PageSize));
        command.Parameters.Add(P("EmpCode", NpgsqlDbType.Varchar, DbStr(query.EmpCode)));

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
        using var command = Function(connection, "dbo.usp_VisitReport_ReportingMapping_UpdateStatus",
            P("Id", NpgsqlDbType.Integer, id),
            P("IsActive", NpgsqlDbType.Boolean, isActive));

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return Convert.ToInt32(reader["RowsAffected"]) > 0;
        return false;
    }
    private static object DbStr(string value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static string ReadStr(DbDataReader reader, string col) => reader[col] == DBNull.Value ? null : Convert.ToString(reader[col])?.Trim();
}
