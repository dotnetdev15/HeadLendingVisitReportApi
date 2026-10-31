namespace HeadLendingVisitReportApi.Models.Entities;

public class AttachmentRecord
{
    public int Id { get; set; }
    public int VisitReportId { get; set; }
    public string FileName { get; set; }
    public string StoredFileName { get; set; }
    public string FilePath { get; set; }
    public string ContentType { get; set; }
    public long FileSizeBytes { get; set; }
    public DateTime UploadedAtUtc { get; set; }
    public string? DocumentCategory { get; set; }
}