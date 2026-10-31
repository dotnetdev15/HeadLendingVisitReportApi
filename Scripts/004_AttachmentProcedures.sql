/* =====================================================================
   Stored Procedures — Attachments (files on disk, metadata in SQL Server)
   ===================================================================== */
USE HeadLendingVisitReportDb;
GO

IF OBJECT_ID('dbo.usp_Attachment_Insert', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Attachment_Insert;
GO
CREATE PROCEDURE dbo.usp_Attachment_Insert
(
    @VisitReportId  INT,
    @FileName       NVARCHAR(260),
    @StoredFileName NVARCHAR(260),
    @FilePath       NVARCHAR(500),
    @ContentType    NVARCHAR(100),
    @FileSizeBytes  BIGINT
)
AS
BEGIN
    SET NOCOUNT ON;

    -- If the parent report doesn't exist, return no result set (ExecuteScalar
    -- comes back NULL) instead of raising an error, so the API can turn this
    -- into a plain 404 rather than an unhandled SqlException.
    IF NOT EXISTS (SELECT 1 FROM dbo.LendingVisitReports WHERE Id = @VisitReportId)
        RETURN;

    INSERT INTO dbo.LendingVisitAttachments
        (VisitReportId, FileName, StoredFileName, FilePath, ContentType, FileSizeBytes)
    VALUES
        (@VisitReportId, @FileName, @StoredFileName, @FilePath, @ContentType, @FileSizeBytes);

    SELECT SCOPE_IDENTITY() AS Id;
END
GO

IF OBJECT_ID('dbo.usp_Attachment_GetByVisitReportId', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Attachment_GetByVisitReportId;
GO
CREATE PROCEDURE dbo.usp_Attachment_GetByVisitReportId
(
    @VisitReportId INT
)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.LendingVisitAttachments WHERE VisitReportId = @VisitReportId ORDER BY UploadedAtUtc;
END
GO

IF OBJECT_ID('dbo.usp_Attachment_GetById', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Attachment_GetById;
GO
CREATE PROCEDURE dbo.usp_Attachment_GetById
(
    @Id INT
)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT * FROM dbo.LendingVisitAttachments WHERE Id = @Id;
END
GO

IF OBJECT_ID('dbo.usp_Attachment_Delete', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_Attachment_Delete;
GO
CREATE PROCEDURE dbo.usp_Attachment_Delete
(
    @Id INT
)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.LendingVisitAttachments WHERE Id = @Id;
    SELECT @@ROWCOUNT AS RowsDeleted;
END
GO
