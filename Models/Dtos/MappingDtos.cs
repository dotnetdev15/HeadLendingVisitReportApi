namespace HeadLendingVisitReportApi.Models.Dtos;

public class CreatorMappingDto
{
    public int Id { get; set; }
    public string EmpCode { get; set; }
    public string Creator { get; set; }
    public string BranchName { get; set; }
    public string BranchCode { get; set; }
    public bool IsActive { get; set; }
}

public class ReportingMappingDto
{
    public int Id { get; set; }
    public string EmpCode { get; set; }
    public string ParentEmpCode { get; set; }
    public string EmpType { get; set; }
    public bool IsActive { get; set; }
}

public class MappingListQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string EmpCode { get; set; }
}

public class UpdateMappingStatusRequest
{
    public int Id { get; set; }
    public bool IsActive { get; set; }
}

public class CreatorMappingUploadRow
{
    public string EmpCode { get; set; }
    public string Creator { get; set; }
    public string BranchName { get; set; }
    public string BranchCode { get; set; }
}

public class CreatorMappingBulkUploadRequest
{
    public List<CreatorMappingUploadRow> Rows { get; set; } = new();
    public string CreatedBy { get; set; }
}

public class BulkUploadResultDto
{
    public int InsertedCount { get; set; }
    public int SkippedCount { get; set; }
}
