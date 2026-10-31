CREATE OR ALTER PROC dbo.usp_VisitReport_UploadDoc
(
    @VisitReportId      INT = NULL,
    @BusinessTypeImg    NVARCHAR(MAX) = NULL,
    @CashTypeImg        NVARCHAR(MAX) = NULL,
    @OdTypeImg          NVARCHAR(MAX) = NULL,
    @NpaTypeImg         NVARCHAR(MAX) = NULL,
    @CollectionTypeImg  NVARCHAR(MAX) = NULL,
    @AuditTypeImg       NVARCHAR(MAX) = NULL,
    @OthersTypeImg      NVARCHAR(MAX) = NULL,
    @Email              NVARCHAR(100) = NULL,
    @Mode               VARCHAR(20)
)
AS
BEGIN
    SET NOCOUNT ON;
    IF (@Mode = 'UPDATE')
    BEGIN
        UPDATE dbo.LendingVisitReports
        SET BusinessTypeImg=@BusinessTypeImg, CashTypeImg=@CashTypeImg, ODTypeImg=@OdTypeImg,
            NPATypeImg=@NpaTypeImg, CollectionTypeImg=@CollectionTypeImg, AuditTypeImg=@AuditTypeImg,
            OthersTypeImg=@OthersTypeImg
        WHERE Id = @VisitReportId;
    END
    IF (@Mode = 'GET')
    BEGIN
        SELECT
            um.[Name] AS [Name],
            m.EmpType AS RoleName,
            um.BranchCode,
            CAST(COALESCE(NULLIF(LTRIM(RTRIM(um.EmpCode)), ''), um.OldEmpCode) AS NVARCHAR(50)) AS EmpCode,
            um.Email
        FROM UsersMasters um
        LEFT JOIN dbo.UserReportingMappingMasters m
            ON ISNULL(m.IsActive, 1) = 1
           AND (
                LTRIM(RTRIM(m.EmpCode)) = CAST(COALESCE(NULLIF(LTRIM(RTRIM(um.EmpCode)), ''), um.OldEmpCode) AS VARCHAR(50))
                OR LTRIM(RTRIM(m.OldEmpCode)) = CAST(COALESCE(NULLIF(LTRIM(RTRIM(um.EmpCode)), ''), um.OldEmpCode) AS VARCHAR(50))
               )
        WHERE um.IsActive = 1 AND um.IsDeleted = 0 AND um.Email = @Email;
    END
END;
GO
