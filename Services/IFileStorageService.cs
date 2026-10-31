namespace HeadLendingVisitReportApi.Services;

public class SavedFileResult
{
    public string StoredFileName { get; set; }
    public string RelativePath { get; set; }
    public long FileSizeBytes { get; set; }
}

public interface IFileStorageService
{
    /// <summary>Saves an uploaded file under a per-report folder on disk and returns its stored details.</summary>
    Task<SavedFileResult> SaveFileAsync(int visitReportId, IFormFile file);

    /// <summary>Opens a read stream for a previously stored file (relative path as recorded in the database).</summary>
    Stream OpenRead(string relativePath);

    /// <summary>Deletes a previously stored file from disk, if it exists.</summary>
    void DeleteFile(string relativePath);
}
