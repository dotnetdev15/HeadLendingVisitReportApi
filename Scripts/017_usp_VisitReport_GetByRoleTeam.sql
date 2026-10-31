CREATE OR ALTER PROCEDURE [dbo].[usp_VisitReport_GetByRoleTeam]
(
    @Page         INT            = 1,
    @PageSize     INT            = 20,
    @BranchName   NVARCHAR(200)  = NULL,
    @EmployeeCode NVARCHAR(50)   = NULL,
    @FromDate     DATETIME2      = NULL,
    @ToDate       DATETIME2      = NULL,
    @Email        NVARCHAR(256)  = NULL,
    @EmpType      NVARCHAR(50)   = NULL,
    @RoleName     NVARCHAR(50)   = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page     < 1 SET @Page     = 1;
    IF @PageSize < 1 SET @PageSize = 20;

    IF LTRIM(RTRIM(ISNULL(@BranchName,   ''))) = '' SET @BranchName   = NULL;
    IF LTRIM(RTRIM(ISNULL(@EmployeeCode, ''))) = '' SET @EmployeeCode = NULL;
    IF LTRIM(RTRIM(ISNULL(@Email,        ''))) = '' SET @Email        = NULL;
    IF LTRIM(RTRIM(ISNULL(@EmpType,      ''))) = '' SET @EmpType      = NULL;
    IF LTRIM(RTRIM(ISNULL(@RoleName,     ''))) = '' SET @RoleName     = NULL;

    IF @EmpType IS NULL SET @EmpType = @RoleName;

    DECLARE @ViewerEmp VARCHAR(50) = NULL;

    IF @Email IS NOT NULL
    BEGIN
        SELECT TOP (1)
            @ViewerEmp = CAST(COALESCE(NULLIF(LTRIM(RTRIM(um.EmpCode)), ''), um.OldEmpCode) AS VARCHAR(50))
        FROM dbo.UsersMasters um
        WHERE um.Email = @Email
          AND ISNULL(um.IsActive, 1) = 1
          AND ISNULL(um.IsDeleted, 0) = 0;
    END

    ;WITH Tree AS
    (
        SELECT
            m.Id,
            LTRIM(RTRIM(m.EmpCode))    AS EmpCode,
            LTRIM(RTRIM(m.OldEmpCode)) AS OldEmpCode,
            LTRIM(RTRIM(m.ParentEmpCode)) AS ParentEmpCode,
            LTRIM(RTRIM(m.EmpType))    AS EmpType,
            0 AS Lvl
        FROM dbo.UserReportingMappingMasters m
        WHERE ISNULL(m.IsActive, 1) = 1
          AND @ViewerEmp IS NOT NULL
          AND (
                LTRIM(RTRIM(m.EmpCode)) = @ViewerEmp
                OR LTRIM(RTRIM(m.OldEmpCode)) = @ViewerEmp
              )

        UNION ALL

        SELECT
            c.Id,
            LTRIM(RTRIM(c.EmpCode)),
            LTRIM(RTRIM(c.OldEmpCode)),
            LTRIM(RTRIM(c.ParentEmpCode)),
            LTRIM(RTRIM(c.EmpType)),
            t.Lvl + 1
        FROM dbo.UserReportingMappingMasters c
        INNER JOIN Tree t
            ON ISNULL(c.IsActive, 1) = 1
           AND t.Lvl < 10
           AND (
                (t.EmpCode IS NOT NULL AND LTRIM(RTRIM(c.ParentEmpCode)) = t.EmpCode)
                OR (t.OldEmpCode IS NOT NULL AND LTRIM(RTRIM(c.ParentEmpCode)) = t.OldEmpCode)
               )
    ),
    RoleEmps AS
    (
        SELECT DISTINCT v.Code
        FROM Tree t
        CROSS APPLY (VALUES (t.EmpCode), (t.OldEmpCode)) AS v(Code)
        WHERE t.Lvl > 0
          AND v.Code IS NOT NULL
          AND v.Code <> ''
          AND (
                @EmpType IS NULL
                OR UPPER(t.EmpType) = UPPER(@EmpType)
              )
    ),
    Filtered AS
    (
        SELECT r.*
        FROM dbo.LendingVisitReports r
        WHERE (@BranchName   IS NULL OR r.BranchName   LIKE N'%' + @BranchName + N'%')
          AND (@EmployeeCode IS NULL OR r.EmployeeCode  = @EmployeeCode)
          AND (@FromDate     IS NULL OR r.VisitDateTime >= @FromDate)
          AND (@ToDate       IS NULL OR r.VisitDateTime <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
          AND @ViewerEmp IS NOT NULL
          AND r.EmployeeCode IN (SELECT Code FROM RoleEmps)
    )
    SELECT *
    FROM Filtered
    ORDER BY CreatedAtUtc DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

    ;WITH Tree AS
    (
        SELECT
            m.Id,
            LTRIM(RTRIM(m.EmpCode))    AS EmpCode,
            LTRIM(RTRIM(m.OldEmpCode)) AS OldEmpCode,
            LTRIM(RTRIM(m.ParentEmpCode)) AS ParentEmpCode,
            LTRIM(RTRIM(m.EmpType))    AS EmpType,
            0 AS Lvl
        FROM dbo.UserReportingMappingMasters m
        WHERE ISNULL(m.IsActive, 1) = 1
          AND @ViewerEmp IS NOT NULL
          AND (
                LTRIM(RTRIM(m.EmpCode)) = @ViewerEmp
                OR LTRIM(RTRIM(m.OldEmpCode)) = @ViewerEmp
              )

        UNION ALL

        SELECT
            c.Id,
            LTRIM(RTRIM(c.EmpCode)),
            LTRIM(RTRIM(c.OldEmpCode)),
            LTRIM(RTRIM(c.ParentEmpCode)),
            LTRIM(RTRIM(c.EmpType)),
            t.Lvl + 1
        FROM dbo.UserReportingMappingMasters c
        INNER JOIN Tree t
            ON ISNULL(c.IsActive, 1) = 1
           AND t.Lvl < 10
           AND (
                (t.EmpCode IS NOT NULL AND LTRIM(RTRIM(c.ParentEmpCode)) = t.EmpCode)
                OR (t.OldEmpCode IS NOT NULL AND LTRIM(RTRIM(c.ParentEmpCode)) = t.OldEmpCode)
               )
    ),
    RoleEmps AS
    (
        SELECT DISTINCT v.Code
        FROM Tree t
        CROSS APPLY (VALUES (t.EmpCode), (t.OldEmpCode)) AS v(Code)
        WHERE t.Lvl > 0
          AND v.Code IS NOT NULL
          AND v.Code <> ''
          AND (
                @EmpType IS NULL
                OR UPPER(t.EmpType) = UPPER(@EmpType)
              )
    ),
    Filtered AS
    (
        SELECT r.Id
        FROM dbo.LendingVisitReports r
        WHERE (@BranchName   IS NULL OR r.BranchName   LIKE N'%' + @BranchName + N'%')
          AND (@EmployeeCode IS NULL OR r.EmployeeCode  = @EmployeeCode)
          AND (@FromDate     IS NULL OR r.VisitDateTime >= @FromDate)
          AND (@ToDate       IS NULL OR r.VisitDateTime <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
          AND @ViewerEmp IS NOT NULL
          AND r.EmployeeCode IN (SELECT Code FROM RoleEmps)
    )
    SELECT COUNT(*) AS TotalCount FROM Filtered;
END
GO
