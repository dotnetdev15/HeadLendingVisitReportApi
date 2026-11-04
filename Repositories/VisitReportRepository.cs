using System.Data;
using System.Data.Common;
using System.Text.Json;
using HeadLendingVisitReportApi.Data;
using HeadLendingVisitReportApi.Models.Dtos;
using Npgsql;
using NpgsqlTypes;
using static HeadLendingVisitReportApi.Data.PostgresCommandBuilder;

namespace HeadLendingVisitReportApi.Repositories;

public class VisitReportRepository : IVisitReportRepository
{
    private readonly IDbConnectionFactory _connectionFactory;
    public VisitReportRepository(IDbConnectionFactory connectionFactory)
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
            command.CommandText = BuildNamedFunctionCall("public.\"usp_VisitReport_Create\"", command.Parameters.Select(parameter => parameter.ParameterName).ToArray());

            await connection.OpenAsync();

            var result = await command.ExecuteScalarAsync();
            return ReadIdFromResult(result, "VisitReportId");
        }
        catch (NpgsqlException ex)
        {
            throw new Exception($"Failed to create visit report: {ex.Message}", ex);
        }
    }
    /// <summary>Reads an id returned either as a plain number or as a jsonb object ({"VisitReportId": n}).</summary>
    private static int ReadIdFromResult(object result, string jsonPropertyName)
    {
        if (result == null || result == DBNull.Value) return 0;
        if (result is string text && text.TrimStart().StartsWith("{"))
        {
            using var resultJson = JsonDocument.Parse(text);
            return GetJsonInt(resultJson.RootElement, jsonPropertyName) ?? GetJsonInt(resultJson.RootElement, "Id") ?? 0;
        }
        return Convert.ToInt32(result);
    }
    public async Task<bool> UpdateAsync(int id, VisitReportDto dto)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = new NpgsqlCommand { Connection = connection };

        command.Parameters.Add(CreateParameter("Id", NpgsqlDbType.Integer, id));
        AddHeaderParameters(command, dto);
        AddTvpParameters(command, dto);
        // Named arguments (p_id => @Id, p_branchname => @BranchName, ...) – same as Create, so order does not matter.
        command.CommandText = BuildNamedFunctionCall("public.\"usp_VisitReport_Update\"", command.Parameters.Select(parameter => parameter.ParameterName).ToArray());

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result != null;
    }
    public async Task<VisitReportDto> GetByIdAsync(int id)
    {
        using var connection = _connectionFactory.CreateConnection();
        // usp_VisitReport_GetById returns 7 cursors = the 7 result sets of the old SP (same order)
        using var command = new NpgsqlCommand(BuildFunctionCall("public.\"usp_VisitReport_GetById\"", "Id"), connection);
        command.Parameters.Add(CreateParameter("Id", NpgsqlDbType.Integer, id));

        await connection.OpenAsync();
        using var reader = await command.ExecuteJsonSetReaderAsync(
     "branchname", "typeofloan", "observation", "recoverymade",
     "borrowername", "auditorobservation", "uploadedatutc");

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

        // public."usp_VisitReport_GetAll" returns SETOF jsonb: one object per report row,
        // with the total in "TotalCount" on every row (old SP's 2nd result set).
        var filterNames = new[] { "BranchName", "EmployeeCode", "FromDate", "ToDate", "Email", "SelectedEmpCode", "ViewRole" };
        using var command = new NpgsqlCommand(BuildFunctionCall("public.\"usp_VisitReport_GetAll\"", new[] { "Page", "PageSize" }.Concat(filterNames).ToArray()), connection);

        command.Parameters.Add(CreateParameter("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(CreateParameter("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 20 : query.PageSize));
        command.Parameters.Add(CreateParameter("BranchName", NpgsqlDbType.Varchar, ToDbString(query.BranchName)));
        command.Parameters.Add(CreateParameter("EmployeeCode", NpgsqlDbType.Varchar, ToDbString(query.EmployeeCode)));
        command.Parameters.Add(CreateParameter("FromDate", NpgsqlDbType.TimestampTz, ParseDbDate(query.FromDate)));
        command.Parameters.Add(CreateParameter("ToDate", NpgsqlDbType.TimestampTz, ParseDbDate(query.ToDate)));
        command.Parameters.Add(CreateParameter("Email", NpgsqlDbType.Varchar, ToDbString(email)));
        command.Parameters.Add(CreateParameter("SelectedEmpCode", NpgsqlDbType.Varchar, ToDbString(query.SelectedEmpCode)));
        command.Parameters.Add(CreateParameter("ViewRole", NpgsqlDbType.Varchar, ToDbString(query.ViewRole)));

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();

        var items = new List<VisitReportDto>();
        var totalCount = 0;
        while (await reader.ReadAsync())
        {
            if (reader.IsDBNull(0)) continue;
            using var rowJson = JsonDocument.Parse(reader.GetString(0));
            var row = rowJson.RootElement;
            items.Add(MapHeaderFromJson(row));
            totalCount = GetJsonInt(row, "TotalCount") ?? totalCount;
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

        using var connection = _connectionFactory.CreateConnection();
        // Named arguments (p_page => @Page, ...). Only parameters that exist in the PostgreSQL function are sent.
        var filterNames = new[] { "BranchName", "EmployeeCode", "FromDate", "ToDate", "Email", "EmpType", "TeamFilter" };
        using var command = new NpgsqlCommand(BuildNamedFunctionCall("public.\"usp_VisitReport_GetByRoleTeam\"", new[] { "Page", "PageSize" }.Concat(filterNames).ToArray()), connection)
        {
            CommandTimeout = 60
        };

        command.Parameters.Add(CreateParameter("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(CreateParameter("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 20 : query.PageSize));
        command.Parameters.Add(CreateParameter("BranchName", NpgsqlDbType.Varchar, ToDbString(query.BranchName)));
        command.Parameters.Add(CreateParameter("EmployeeCode", NpgsqlDbType.Varchar, ToDbString(query.EmployeeCode)));
        command.Parameters.Add(CreateParameter("FromDate", NpgsqlDbType.Timestamp, ParseDbDate(query.FromDate)));
        command.Parameters.Add(CreateParameter("ToDate", NpgsqlDbType.Timestamp, ParseDbDate(query.ToDate)));
        command.Parameters.Add(CreateParameter("Email", NpgsqlDbType.Varchar, ToDbString(email)));
        command.Parameters.Add(CreateParameter("EmpType", NpgsqlDbType.Varchar, ToDbString(empType)));
        // Logged-in user's EmpCode (old @EmpCode), sent in p_teamfilter: the function builds this user's team from it
        command.Parameters.Add(CreateParameter("TeamFilter", NpgsqlDbType.Text, ToDbString(FirstNonEmpty(query.EmpCode, query.empCode))));

        await connection.OpenAsync();

        // Function returns SETOF jsonb (one row per report, "TotalCount" inside every row)
        using var reader = await command.ExecuteJsonSetReaderAsync("branchname");
        var items = new List<VisitReportDto>();
        var totalCount = 0;
        while (await reader.ReadAsync())
        {
            items.Add(MapHeader(reader));
            if (totalCount == 0) totalCount = reader.GetInt32Value("TotalCount");
        }
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
        using var command = new NpgsqlCommand(BuildFunctionCall("public.\"usp_VisitReport_AdminVisitSummary\"", new[] { "Page", "PageSize" }.Concat(filterNames).ToArray()), connection)
        {
            CommandTimeout = 60
        };

        command.Parameters.Add(CreateParameter("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(CreateParameter("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 10 : query.PageSize));
        command.Parameters.Add(CreateParameter("FromDate", NpgsqlDbType.Timestamp, query.FromDate));
        command.Parameters.Add(CreateParameter("ToDate", NpgsqlDbType.Timestamp, query.ToDate));
        command.Parameters.Add(CreateParameter("EmpType", NpgsqlDbType.Varchar, ToDbString(query.EmpType)));
        command.Parameters.Add(CreateParameter("Creator", NpgsqlDbType.Varchar, ToDbString(query.Creator)));
        command.Parameters.Add(CreateParameter("BranchCode", NpgsqlDbType.Varchar, ToDbString(query.BranchCode)));
        command.Parameters.Add(CreateParameter("EmployeeCode", NpgsqlDbType.Varchar, ToDbString(query.EmployeeCode)));

        await connection.OpenAsync();
        using var reader = await command.ExecuteJsonSetReaderAsync("empcode", "totalcount");

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
        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_AdminVisitDetails\"",
            CreateParameter("FromDate", NpgsqlDbType.Timestamp, query.FromDate),
            CreateParameter("ToDate", NpgsqlDbType.Timestamp, query.ToDate),
            CreateParameter("EmployeeCode", NpgsqlDbType.Varchar, ToDbString(query.EmployeeCode)),
            CreateParameter("EmpType", NpgsqlDbType.Varchar, ToDbString(query.EmpType)),
            CreateParameter("Creator", NpgsqlDbType.Varchar, ToDbString(query.Creator)),
            CreateParameter("BranchCode", NpgsqlDbType.Varchar, ToDbString(query.BranchCode)));
        command.CommandTimeout = 60;

        await connection.OpenAsync();
        // Works whether the DB function returns RETURNS TABLE columns or SETOF jsonb
        using var reader = await command.ExecuteJsonSetReaderAsync();

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
        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_Delete\"",
            CreateParameter("Id", NpgsqlDbType.Integer, id));

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return result == null ? 0 : Convert.ToInt32(result);
    }
    private static void AddHeaderParameters(NpgsqlCommand command, VisitReportDto dto)
    {
        command.Parameters.Add(CreateParameter("BranchName", NpgsqlDbType.Varchar, dto.BranchName));
        command.Parameters.Add(CreateParameter("HeadName", NpgsqlDbType.Varchar, dto.HeadName));
        command.Parameters.Add(CreateParameter("EmployeeCode", NpgsqlDbType.Varchar, dto.EmployeeCode));
        command.Parameters.Add(CreateParameter("VisitDateTime", NpgsqlDbType.Timestamp, dto.VisitDateTime));

        command.Parameters.Add(CreateParameter("DemandForMonth", NpgsqlDbType.Numeric, dto.DemandForMonth));
        command.Parameters.Add(CreateParameter("CollectionAmount", NpgsqlDbType.Numeric, dto.CollectionAmount));
        command.Parameters.Add(CreateParameter("Shortfall", NpgsqlDbType.Numeric, dto.Shortfall));
        command.Parameters.Add(CreateParameter("CollectionPercent", NpgsqlDbType.Numeric, dto.CollectionPercent));
        command.Parameters.Add(CreateParameter("CollectionRemarks", NpgsqlDbType.Varchar, dto.CollectionRemarks));

        command.Parameters.Add(CreateParameter("ObsBusiness", NpgsqlDbType.Text, dto.ObsBusiness));
        command.Parameters.Add(CreateParameter("ObsCash", NpgsqlDbType.Text, dto.ObsCash));
        command.Parameters.Add(CreateParameter("ObsOd", NpgsqlDbType.Text, dto.ObsOd));
        command.Parameters.Add(CreateParameter("ObsNpa", NpgsqlDbType.Text, dto.ObsNpa));
        command.Parameters.Add(CreateParameter("ObsCollection", NpgsqlDbType.Text, dto.ObsCollection));
        command.Parameters.Add(CreateParameter("ObsAudit", NpgsqlDbType.Text, dto.ObsAudit));
        command.Parameters.Add(CreateParameter("ObsOthers", NpgsqlDbType.Text, dto.ObsOthers));

        command.Parameters.Add(CreateParameter("SignatureName", NpgsqlDbType.Varchar, dto.SignatureName));
        command.Parameters.Add(CreateParameter("SignatureDate", NpgsqlDbType.Date, dto.SignatureDate?.Date));
    }
    private static void AddTvpParameters(NpgsqlCommand command, VisitReportDto dto)
    {
        AddTvp(command, "BusinessDevelopments", "public.\"BusinessDevelopmentRowType\"", ToBusinessDevelopmentTable(dto.BusinessDevelopments));
        AddTvp(command, "CashManagements", "public.\"CashManagementRowType\"", ToCashManagementTable(dto.CashManagements));
        AddTvp(command, "NpaOdManagements", "public.\"NpaOdManagementRowType\"", ToNpaOdManagementTable(dto.NpaOdManagements));
        AddTvp(command, "NpaBorrowers", "public.\"NpaBorrowerRowType\"", ToNpaBorrowerTable(dto.NpaBorrowers));
        AddTvp(command, "ConcurrentAudits", "public.\"ConcurrentAuditRowType\"", ToConcurrentAuditTable(dto.ConcurrentAudits));
    }
    /// <summary>
    /// PostgreSQL has no Table-Valued Parameters: the row set is sent as a JSONB
    /// array and expanded with jsonb_to_recordset inside the function.
    /// typeName is kept only to document which old SQL Server table type it replaces.
    /// </summary>
    private static void AddTvp(NpgsqlCommand command, string paramName, string typeName, DataTable table)
    {
        _ = typeName;
        command.Parameters.Add(CreateJsonParameter(paramName, table));
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
    /// <summary>Same mapping as MapHeader, for a report row returned as jsonb.</summary>
    private static VisitReportDto MapHeaderFromJson(JsonElement row) => new()
    {
        Id = GetJsonInt(row, "Id") ?? 0,
        BranchName = GetJsonString(row, "BranchName"),
        HeadName = GetJsonString(row, "HeadName"),
        EmployeeCode = GetJsonString(row, "EmployeeCode"),
        VisitDateTime = GetJsonDateTime(row, "VisitDateTime"),

        DemandForMonth = GetJsonDecimal(row, "DemandForMonth"),
        CollectionAmount = GetJsonDecimal(row, "CollectionAmount"),
        Shortfall = GetJsonDecimal(row, "Shortfall"),
        CollectionPercent = GetJsonDecimal(row, "CollectionPercent"),
        CollectionRemarks = GetJsonString(row, "CollectionRemarks"),

        ObsBusiness = GetJsonString(row, "ObsBusiness"),
        ObsCash = GetJsonString(row, "ObsCash"),
        ObsOd = GetJsonString(row, "ObsOd"),
        ObsNpa = GetJsonString(row, "ObsNpa"),
        ObsCollection = GetJsonString(row, "ObsCollection"),
        ObsAudit = GetJsonString(row, "ObsAudit"),
        ObsOthers = GetJsonString(row, "ObsOthers"),

        BusinessTypeImg = GetJsonString(row, "BusinessTypeImg"),
        CashTypeImg = GetJsonString(row, "CashTypeImg"),
        OdTypeImg = GetJsonString(row, "OdTypeImg"),
        NpaTypeImg = GetJsonString(row, "NpaTypeImg"),
        CollectionTypeImg = GetJsonString(row, "CollectionTypeImg"),
        AuditTypeImg = GetJsonString(row, "AuditTypeImg"),
        OthersTypeImg = GetJsonString(row, "OthersTypeImg"),

        FormRole = GetJsonString(row, "FormRole"),

        SignatureName = GetJsonString(row, "SignatureName"),
        SignatureDate = GetJsonDateTime(row, "SignatureDate"),

        CreatedAtUtc = GetJsonDateTime(row, "CreatedAtUtc"),
        UpdatedAtUtc = GetJsonDateTime(row, "UpdatedAtUtc"),
    };
    private static decimal? GetJsonDecimal(JsonElement element, string propertyName)
    {
        if (!TryGetJsonProperty(element, propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out number)) return number;
        return null;
    }
    private static DateTime? GetJsonDateTime(JsonElement element, string propertyName)
    {
        var text = GetJsonString(element, propertyName);
        if (string.IsNullOrWhiteSpace(text)) return null;
        return DateTime.TryParse(text, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date) ? date : null;
    }
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

        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_UploadDoc\"",
            CreateParameter("VisitReportId", NpgsqlDbType.Integer, visitReportId),
            CreateParameter("BusinessTypeImg", NpgsqlDbType.Text, dto.BusinessTypeImg),
            CreateParameter("CashTypeImg", NpgsqlDbType.Text, dto.CashTypeImg),
            CreateParameter("OdTypeImg", NpgsqlDbType.Text, dto.OdTypeImg),
            CreateParameter("NpaTypeImg", NpgsqlDbType.Text, dto.NpaTypeImg),
            CreateParameter("CollectionTypeImg", NpgsqlDbType.Text, dto.CollectionTypeImg),
            CreateParameter("AuditTypeImg", NpgsqlDbType.Text, dto.AuditTypeImg),
            CreateParameter("OthersTypeImg", NpgsqlDbType.Text, dto.OthersTypeImg),
            CreateParameter("Email", NpgsqlDbType.Varchar, DBNull.Value),
            CreateParameter("Mode", NpgsqlDbType.Varchar, "UPDATE"));
        await connection.OpenAsync();
        await command.ExecuteNonQueryAsync();
    }
    public async Task<LoginProfileDto> GetLoginProfileByEmpCodeAsync(string empCode)
    {
        if (string.IsNullOrWhiteSpace(empCode)) return null;

        using var connection = _connectionFactory.CreateConnection();
        // public."usp_VisitReport_GetLoginProfile"(text) returns SETOF jsonb:
        // the profile row with the branch list (old 2nd result set) in "branches".
        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_GetLoginProfile\"",
            CreateParameter("EmpCode", NpgsqlDbType.Text, empCode.Trim()));

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (!await reader.ReadAsync() || reader.IsDBNull(0)) return null;

        using var profileJson = JsonDocument.Parse(reader.GetString(0));
        var profileRow = profileJson.RootElement;

        var empType = GetJsonString(profileRow, "EmpType") ?? GetJsonString(profileRow, "RoleName");
        var roleName = empType ?? GetJsonString(profileRow, "RoleName");
        int? roleId = GetJsonInt(profileRow, "RoleId");

        var creator = GetJsonString(profileRow, "Creator");
        var branchCode = GetJsonString(profileRow, "BranchCode");
        var branchName = GetJsonString(profileRow, "BranchName");

        var profile = new LoginProfileDto
        {
            Id = GetJsonInt(profileRow, "Id") ?? 0,
            RoleId = roleId,
            Email = GetJsonString(profileRow, "Email"),
            Name = GetJsonString(profileRow, "Name"),
            RoleName = roleName,
            FormRole = roleName,
            EmpType = empType,
            BranchCode = branchCode,
            BranchName = branchName,
            Creator = creator,
            EmpCode = GetJsonString(profileRow, "EmpCode") ?? empCode.Trim()
        };

        if (TryGetJsonProperty(profileRow, "Branches", out var branchesElement) && branchesElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var branchElement in branchesElement.EnumerateArray())
            {
                var item = branchElement.ValueKind == JsonValueKind.String ? branchElement.GetString() : null;
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
    /// <summary>jsonb keys from to_jsonb() are lower-case; match property names case-insensitively.</summary>
    private static bool TryGetJsonProperty(JsonElement element, string propertyName, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, propertyName, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
    private static string GetJsonString(JsonElement element, string propertyName)
    {
        if (!TryGetJsonProperty(element, propertyName, out var value)) return null;
        return value.ValueKind switch
        {
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            JsonValueKind.String => value.GetString(),
            _ => value.ToString()
        };
    }
    private static int? GetJsonInt(JsonElement element, string propertyName)
    {
        if (!TryGetJsonProperty(element, propertyName, out var value)) return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number)) return number;
        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number)) return number;
        return null;
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
        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_AddComment\"",
            CreateParameter("VisitReportId", NpgsqlDbType.Integer, visitReportId),
            CreateParameter("EmployeeCode", NpgsqlDbType.Varchar, req.EmployeeCode),
            CreateParameter("CommentText", NpgsqlDbType.Text, req.CommentText ?? ""),
            CreateParameter("CreatedByEmail", NpgsqlDbType.Varchar, req.CreatedByEmail),
            CreateParameter("CreatedByName", NpgsqlDbType.Varchar, req.CreatedByName));

        await connection.OpenAsync();
        var result = await command.ExecuteScalarAsync();
        return Convert.ToInt32(result);
    }
    public async Task<List<VisitReportCommentDto>> GetCommentsAsync(int visitReportId)
    {
        using var connection = _connectionFactory.CreateConnection();
        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_GetComments\"",
            CreateParameter("VisitReportId", NpgsqlDbType.Integer, visitReportId));

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
        using var command = new NpgsqlCommand(BuildFunctionCall("public.\"usp_VisitReport_CreatorMapping_List\"", "Page", "PageSize", "EmpCode"), connection)
        {
            CommandTimeout = 60
        };
        command.Parameters.Add(CreateParameter("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(CreateParameter("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 10 : query.PageSize));
        command.Parameters.Add(CreateParameter("EmpCode", NpgsqlDbType.Varchar, ToDbString(query.EmpCode)));

        await connection.OpenAsync();
        using var reader = await command.ExecuteJsonSetReaderAsync("empcode", "totalcount");

        var items = new List<CreatorMappingDto>();
        while (await reader.ReadAsync())
        {
            items.Add(new CreatorMappingDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                EmpCode = ReadTrimmedString(reader, "EmpCode"),
                Creator = ReadTrimmedString(reader, "Creator"),
                BranchName = ReadTrimmedString(reader, "BranchName"),
                BranchCode = ReadTrimmedString(reader, "BranchCode"),
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
        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_CreatorMapping_UpdateStatus\"",
            CreateParameter("Id", NpgsqlDbType.Integer, id),
            CreateParameter("IsActive", NpgsqlDbType.Boolean, isActive));

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
        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_CreatorMapping_BulkUpload\"",
            CreateJsonParameter("Rows", table),
            CreateParameter("CreatedBy", NpgsqlDbType.Varchar,
                string.IsNullOrWhiteSpace(request.CreatedBy) ? DBNull.Value : request.CreatedBy.Trim()));
        command.CommandTimeout = 120;

        await connection.OpenAsync();
       // using var reader = await command.ExecuteReaderAsync();
        using var reader = await command.ExecuteJsonSetReaderAsync();
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
        using var command = new NpgsqlCommand(BuildFunctionCall("public.\"usp_VisitReport_ReportingMapping_List\"", "Page", "PageSize", "EmpCode"), connection)
        {
            CommandTimeout = 60
        };
        command.Parameters.Add(CreateParameter("Page", NpgsqlDbType.Integer, query.Page < 1 ? 1 : query.Page));
        command.Parameters.Add(CreateParameter("PageSize", NpgsqlDbType.Integer, query.PageSize < 1 ? 10 : query.PageSize));
        command.Parameters.Add(CreateParameter("EmpCode", NpgsqlDbType.Varchar, ToDbString(query.EmpCode)));

        await connection.OpenAsync();
        using var reader = await command.ExecuteJsonSetReaderAsync("empcode", "totalcount");

        var items = new List<ReportingMappingDto>();
        while (await reader.ReadAsync())
        {
            items.Add(new ReportingMappingDto
            {
                Id = Convert.ToInt32(reader["Id"]),
                EmpCode = ReadTrimmedString(reader, "EmpCode"),
                ParentEmpCode = ReadTrimmedString(reader, "ParentEmpCode"),
                EmpType = ReadTrimmedString(reader, "EmpType"),
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
        using var command = CreateFunctionCommand(connection, "public.\"usp_VisitReport_ReportingMapping_UpdateStatus\"",
            CreateParameter("Id", NpgsqlDbType.Integer, id),
            CreateParameter("IsActive", NpgsqlDbType.Boolean, isActive));

        await connection.OpenAsync();
        using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
            return Convert.ToInt32(reader["RowsAffected"]) > 0;
        return false;
    }
    private static object ToDbString(string value) => string.IsNullOrWhiteSpace(value) ? DBNull.Value : value.Trim();
    private static string ReadTrimmedString(DbDataReader reader, string columnName) => reader[columnName] == DBNull.Value ? null : Convert.ToString(reader[columnName])?.Trim();
}
