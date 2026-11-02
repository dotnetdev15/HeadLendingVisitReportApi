namespace HeadLendingVisitReportApi.Models.Dtos;

public class LoginProfileDto
{
    public int? Id { get; set; }
    public int? RoleId { get; set; }
    public string Email { get; set; }
    public string Name { get; set; }
    public string RoleName { get; set; }
    public string FormRole { get; set; }
    public string EmpType { get; set; }
    public string BranchCode { get; set; }
    public string BranchName { get; set; }
    public string Creator { get; set; }
    public List<string> Branches { get; set; } = new();
    public string EmpCode { get; set; }
    public string Department { get; set; }
    public string Designation { get; set; }
    public string ReportingManagerName { get; set; }
    public string EmployeeName => Name;
    public string EmployeeCode => EmpCode;
    public string Role => RoleName;
    public string DeptName => Department;
}
public class BusinessDevelopmentRowDto
{
    public int RowOrder { get; set; }
    public string TypeOfLoan { get; set; }
    public decimal? Budget { get; set; }
    public decimal? Sanctioned { get; set; }
    public decimal? Disbursed { get; set; }
}
public class CashManagementRowDto
{
    public int RowOrder { get; set; }
    public string Particulars { get; set; }
    public string Observation { get; set; }
    public string Remarks { get; set; }
}
public class NpaOdManagementRowDto
{
    public int RowOrder { get; set; }
    public string Particulars { get; set; }
    public decimal? Amount { get; set; }
    public decimal? RecoveryMade { get; set; }
    public string Remarks { get; set; }
}
public class NpaBorrowerRowDto
{
    public int RowOrder { get; set; }
    public string BorrowerName { get; set; }
    public string AccountNo { get; set; }
    public decimal? AmountRecovered { get; set; }
    public string Remarks { get; set; }
}
public class ConcurrentAuditRowDto
{
    public int RowOrder { get; set; }
    public string AuditorObservation { get; set; }
    public string BranchCompliance { get; set; }
    public string Remarks { get; set; }
}
public class AttachmentDto
{
    public int Id { get; set; }
    public int VisitReportId { get; set; }
    public string FileName { get; set; }
    public string FilePath { get; set; }
    public string DocumentCategory { get; set; }
    public string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
}
public class VisitReportDto
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string BranchName { get; set; }
    public string HeadName { get; set; }
    public string EmployeeCode { get; set; }
    public DateTime? VisitDateTime { get; set; }

    public decimal? DemandForMonth { get; set; }
    public decimal? CollectionAmount { get; set; }
    public decimal? Shortfall { get; set; }
    public decimal? CollectionPercent { get; set; }
    public string CollectionRemarks { get; set; }
    public string ObsBusiness { get; set; }
    public string ObsCash { get; set; }
    public string ObsOd { get; set; }
    public string ObsNpa { get; set; }
    public string ObsCollection { get; set; }
    public string ObsAudit { get; set; }
    public string ObsOthers { get; set; }
    public string BusinessTypeImg { get; set; }
    public string CashTypeImg { get; set; }
    public string OdTypeImg { get; set; }
    public string NpaTypeImg { get; set; }
    public string CollectionTypeImg { get; set; }
    public string AuditTypeImg { get; set; }
    public string OthersTypeImg { get; set; }
    public string FormRole { get; set; }
    public string SignatureName { get; set; }
    public DateTime? SignatureDate { get; set; }
    public DateTime? CreatedAtUtc { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public List<BusinessDevelopmentRowDto> BusinessDevelopments { get; set; } = new();
    public List<CashManagementRowDto> CashManagements { get; set; } = new();
    public List<NpaOdManagementRowDto> NpaOdManagements { get; set; } = new();
    public List<NpaBorrowerRowDto> NpaBorrowers { get; set; } = new();
    public List<ConcurrentAuditRowDto> ConcurrentAudits { get; set; } = new();
    public List<AttachmentDto> Attachments { get; set; } = new();
    [System.Text.Json.Serialization.JsonIgnore]   // files come only via multipart (Create); JSON (Update) ignores them
    public List<IFormFile> Files { get; set; } = new();
    public List<string> Categories { get; set; } = new();
}
public class PagedResult<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Page { get; set; }
    public int PageSize { get; set; }
}
//public class VisitReportQuery
//{
//    public string Email { get; set; }
//    public string email
//    {
//        get => Email;
//        set { if (!string.IsNullOrWhiteSpace(value)) Email = value; }
//    }
//    public string ViewerEmail
//    {
//        get => Email;
//        set { if (!string.IsNullOrWhiteSpace(value)) Email = value; }
//    }
//    public int Page { get; set; } = 1;
//    public int PageSize { get; set; } = 20;
//    public string BranchName { get; set; }
//    public string EmployeeCode { get; set; }
//    public DateTime? FromDate { get; set; }
//    public DateTime? ToDate { get; set; }
//    public string SelectedEmpCode { get; set; }
//    public string ViewRole { get; set; }
//    public string Search { get; set; }
//    public string EmpType { get; set; }
//    public string RoleName { get; set; }
//}

public class VisitReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public string FromDate { get; set; }
    public string ToDate { get; set; }
    public string BranchName { get; set; }
    public string EmployeeCode { get; set; }
    public string EmpCode { get; set; }
    public string empCode { get; set; }
    public string Search { get; set; }
    public string EmpType { get; set; }
    public string ViewRole { get; set; }
    public string Email { get; set; }
    public string ViewerEmail { get; set; }
    public string email { get; set; }
    public string SelectedEmpCode { get; set; }
}

public class AdminVisitReportQuery
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public string EmpType { get; set; }
    public string Creator { get; set; }
    public string BranchCode { get; set; }
    public string EmployeeCode { get; set; }
    public string EmpCode
    {
        get => EmployeeCode;
        set { if (!string.IsNullOrWhiteSpace(value)) EmployeeCode = value; }
    }
}

public class AdminVisitSummaryDto
{
    public string Name { get; set; }
    public string EmpCode { get; set; }
    public int VisitCount { get; set; }
    public int AssignedBranchCount { get; set; }
    public int RequiredVisitCount { get; set; }
    public bool RequirementMet { get; set; }
}

public class AdminVisitDetailDto
{
    public int VisitReportId { get; set; }
    public string BranchCode { get; set; }
    public string BranchName { get; set; }
    public string Creator { get; set; }
    public DateTime? VisitDateTime { get; set; }
    public string Name { get; set; }
    public string EmpCode { get; set; }
}

public class VisitReportCommentDto
{
    public int Id { get; set; }
    public int VisitReportId { get; set; }
    public string EmployeeCode { get; set; }
    public string CommentText { get; set; }
    public string CreatedByEmail { get; set; }
    public string CreatedByName { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}
public class AddVisitReportCommentRequest
{
    public string CommentText { get; set; }
    public string EmployeeCode { get; set; }
    public string CreatedByEmail { get; set; }
    public string CreatedByName { get; set; }
}
public class VisitReportPagedResult
{
    public List<VisitReportDto> Items { get; set; }
        = new();
    public int TotalCount { get; set; }
}