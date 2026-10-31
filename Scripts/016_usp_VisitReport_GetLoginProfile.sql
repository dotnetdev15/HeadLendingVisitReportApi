CREATE OR ALTER PROCEDURE dbo.usp_VisitReport_GetLoginProfile
(
    @Email NVARCHAR(256)
)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @EmpCode VARCHAR(50) = NULL;

    SELECT TOP (1)
        @EmpCode = CAST(COALESCE(NULLIF(LTRIM(RTRIM(um.EmpCode)), ''), um.OldEmpCode) AS VARCHAR(50))
    FROM dbo.UsersMasters um
    WHERE um.Email = @Email
      AND ISNULL(um.IsActive, 1) = 1
      AND ISNULL(um.IsDeleted, 0) = 0;

    SELECT TOP (1)
        um.Id,
        um.Email,
        um.[Name],
        CAST(NULL AS INT) AS RoleId,
        m.EmpType AS RoleName,
        m.EmpType AS FormRole,
        m.EmpType AS EmpType,
        um.BranchCode,
        CAST(NULL AS NVARCHAR(200)) AS BranchName,
        @EmpCode AS EmpCode,
        m.ParentEmpCode
    FROM dbo.UsersMasters um
    LEFT JOIN dbo.UserReportingMappingMasters m
        ON ISNULL(m.IsActive, 1) = 1
       AND (
            LTRIM(RTRIM(m.EmpCode)) = @EmpCode
            OR LTRIM(RTRIM(m.OldEmpCode)) = @EmpCode
       )
    WHERE um.Email = @Email
      AND ISNULL(um.IsActive, 1) = 1
      AND ISNULL(um.IsDeleted, 0) = 0;
END
GO
