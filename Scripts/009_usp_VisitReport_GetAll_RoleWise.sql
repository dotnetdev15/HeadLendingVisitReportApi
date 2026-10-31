USE [SBIPDLCOL]
GO
SET ANSI_NULLS ON
GO
SET QUOTED_IDENTIFIER ON
GO

/*
  usp_VisitReport_GetAll — own reports for HeadLendingVisitReport form.

  Email -> EmpCode from UsersMasters only:
    COALESCE(NULLIF(EmpCode, ''), OldEmpCode)

  RolesMasters is not used.
  VisitReports role tabs use usp_VisitReport_GetByRoleTeam (ParentEmpCode + EmpType).
*/
CREATE OR ALTER PROCEDURE [dbo].[usp_VisitReport_GetAll]
(
    @Page            INT            = 1,
    @PageSize        INT            = 20,
    @BranchName      NVARCHAR(200)  = NULL,
    @EmployeeCode    NVARCHAR(50)   = NULL,
    @FromDate        DATETIME2      = NULL,
    @ToDate          DATETIME2      = NULL,
    @Email           NVARCHAR(256)  = NULL,
    @SelectedEmpCode NVARCHAR(50)   = NULL,
    @ViewRole        NVARCHAR(50)   = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page    < 1 SET @Page    = 1;
    IF @PageSize < 1 SET @PageSize = 20;

    IF LTRIM(RTRIM(ISNULL(@BranchName,      ''))) = '' SET @BranchName      = NULL;
    IF LTRIM(RTRIM(ISNULL(@EmployeeCode,    ''))) = '' SET @EmployeeCode    = NULL;
    IF LTRIM(RTRIM(ISNULL(@Email,           ''))) = '' SET @Email           = NULL;
    IF LTRIM(RTRIM(ISNULL(@SelectedEmpCode, ''))) = '' SET @SelectedEmpCode = NULL;
    IF LTRIM(RTRIM(ISNULL(@ViewRole,        ''))) = '' SET @ViewRole        = NULL;

    DECLARE @ViewerEmp NVARCHAR(50) = NULL;

    IF @Email IS NOT NULL
    BEGIN
        SELECT TOP (1)
            @ViewerEmp = CAST(COALESCE(NULLIF(LTRIM(RTRIM(um.EmpCode)), ''), um.OldEmpCode) AS NVARCHAR(50))
        FROM dbo.UsersMasters um
        WHERE um.Email = @Email
          AND ISNULL(um.IsActive,  1) = 1
          AND ISNULL(um.IsDeleted, 0) = 0;
    END

    ;WITH Filtered AS
    (
        SELECT r.*
        FROM dbo.LendingVisitReports r
        WHERE (@BranchName      IS NULL OR r.BranchName   LIKE N'%' + @BranchName + N'%')
          AND (@EmployeeCode    IS NULL OR r.EmployeeCode = @EmployeeCode)
          AND (@FromDate        IS NULL OR r.VisitDateTime >= @FromDate)
          AND (@ToDate          IS NULL OR r.VisitDateTime <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
          AND (@SelectedEmpCode IS NULL OR r.EmployeeCode = @SelectedEmpCode)
          AND (@Email           IS NULL OR (@ViewerEmp IS NOT NULL AND r.EmployeeCode = @ViewerEmp))
    )
    SELECT *
    FROM Filtered
    ORDER BY CreatedAtUtc DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

    ;WITH Filtered AS
    (
        SELECT r.Id
        FROM dbo.LendingVisitReports r
        WHERE (@BranchName      IS NULL OR r.BranchName   LIKE N'%' + @BranchName + N'%')
          AND (@EmployeeCode    IS NULL OR r.EmployeeCode = @EmployeeCode)
          AND (@FromDate        IS NULL OR r.VisitDateTime >= @FromDate)
          AND (@ToDate          IS NULL OR r.VisitDateTime <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
          AND (@SelectedEmpCode IS NULL OR r.EmployeeCode = @SelectedEmpCode)
          AND (@Email           IS NULL OR (@ViewerEmp IS NOT NULL AND r.EmployeeCode = @ViewerEmp))
    )
    SELECT COUNT(*) AS TotalCount FROM Filtered;
END
GO
