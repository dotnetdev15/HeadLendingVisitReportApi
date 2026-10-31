namespace HeadLendingVisitReportApi.Services;

/// <summary>
/// Stores uploaded files on the local file system, under
/// {RootPath}/{visitReportId}/{guid}_{originalFileName}. Only the
/// relative path is ever persisted to PostgreSQL — this class is the
/// only place that knows the absolute disk location.
/// </summary>
public class FileStorageService : IFileStorageService
{
    private readonly string _rootPath;

    public FileStorageService(IConfiguration configuration, IWebHostEnvironment environment)
    {
        var configuredRoot = configuration["FileStorage:RootPath"] ?? "App_Data/Uploads";
        _rootPath = Path.IsPathRooted(configuredRoot)
            ? configuredRoot
            : Path.Combine(environment.ContentRootPath, configuredRoot);

        Directory.CreateDirectory(_rootPath);
    }

    public async Task<SavedFileResult> SaveFileAsync(int visitReportId, IFormFile file)
    {
        if (file is null || file.Length == 0)
            throw new ArgumentException("No file content was provided.", nameof(file));

        var reportFolder = Path.Combine(_rootPath, visitReportId.ToString());
        Directory.CreateDirectory(reportFolder);

        var safeOriginalName = Path.GetFileName(file.FileName);
        var storedFileName = $"{Guid.NewGuid():N}_{safeOriginalName}";
        var absolutePath = Path.Combine(reportFolder, storedFileName);

        await using (var stream = new FileStream(absolutePath, FileMode.Create, FileAccess.Write))
        {
            await file.CopyToAsync(stream);
        }

        var relativePath = Path.Combine(visitReportId.ToString(), storedFileName).Replace('\\', '/');

        return new SavedFileResult
        {
            StoredFileName = storedFileName,
            RelativePath = relativePath,
            FileSizeBytes = file.Length
        };
    }

    public Stream OpenRead(string relativePath)
    {
        var absolutePath = ResolveAbsolutePath(relativePath);
        if (!File.Exists(absolutePath))
            throw new FileNotFoundException("Stored file could not be found on disk.", absolutePath);

        return new FileStream(absolutePath, FileMode.Open, FileAccess.Read);
    }

    public void DeleteFile(string relativePath)
    {
        var absolutePath = ResolveAbsolutePath(relativePath);
        if (File.Exists(absolutePath))
        {
            File.Delete(absolutePath);
        }
    }

    private string ResolveAbsolutePath(string relativePath)
    {
        // Guard against path traversal — the resolved path must stay under the configured root.
        var combined = Path.GetFullPath(Path.Combine(_rootPath, relativePath));
        var rootFull = Path.GetFullPath(_rootPath);
        if (!combined.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
            throw new UnauthorizedAccessException("Resolved file path escapes the configured storage root.");

        return combined;
    }
}
