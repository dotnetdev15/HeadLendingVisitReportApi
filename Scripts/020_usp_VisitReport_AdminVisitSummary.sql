/* =====================================================================
   Admin Visit Report — Summary list (Name | EmpCode | VisitCount)
   Filters: FromDate*, ToDate*, EmpType (HEAD/VP/ZM/RM), Creator, BranchCode, EmployeeCode
   ===================================================================== */
CREATE OR ALTER PROCEDURE dbo.usp_VisitReport_AdminVisitSummary
(
    @Page         INT            = 1,
    @PageSize     INT            = 10,
    @FromDate     DATETIME2      = NULL,
    @ToDate       DATETIME2      = NULL,
    @EmpType      NVARCHAR(50)   = NULL,
    @Creator      NVARCHAR(100)  = NULL,
    @BranchCode   NVARCHAR(50)   = NULL,
    @EmployeeCode NVARCHAR(50)   = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page     < 1 SET @Page     = 1;
    IF @PageSize < 1 SET @PageSize = 10;

    IF LTRIM(RTRIM(ISNULL(@EmpType,      N''))) = N'' SET @EmpType      = NULL;
    IF LTRIM(RTRIM(ISNULL(@Creator,      N''))) = N'' SET @Creator      = NULL;
    IF LTRIM(RTRIM(ISNULL(@BranchCode,   N''))) = N'' SET @BranchCode   = NULL;
    IF LTRIM(RTRIM(ISNULL(@EmployeeCode, N''))) = N'' SET @EmployeeCode = NULL;

    IF @EmpType IS NOT NULL
        SET @EmpType = UPPER(LTRIM(RTRIM(@EmpType)));

    ;WITH Filtered AS
    (
        SELECT
            LTRIM(RTRIM(r.EmployeeCode)) AS EmpCode,
            MAX(NULLIF(LTRIM(RTRIM(r.HeadName)), N'')) AS Name,
            COUNT(1) AS VisitCount
        FROM dbo.LendingVisitReports r
        OUTER APPLY
        (
            SELECT TOP (1) um.EmpType
            FROM dbo.UserReportingMapping_VisitReport um
            WHERE ISNULL(um.IsActive, 1) = 1
              AND LTRIM(RTRIM(um.EmpCode)) = LTRIM(RTRIM(r.EmployeeCode))
            ORDER BY um.Id DESC
        ) map
        WHERE NULLIF(LTRIM(RTRIM(r.EmployeeCode)), N'') IS NOT NULL
          AND (@FromDate IS NULL OR r.VisitDateTime >= @FromDate)
          AND (@ToDate   IS NULL OR r.VisitDateTime <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
          AND (
                @EmpType IS NULL
                OR UPPER(LTRIM(RTRIM(ISNULL(r.FormRole, map.EmpType)))) = @EmpType
              )
          AND (
                @EmployeeCode IS NULL
                OR LTRIM(RTRIM(r.EmployeeCode)) = LTRIM(RTRIM(@EmployeeCode))
              )
          AND (
                @BranchCode IS NULL
                OR LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(@BranchCode))
                OR LTRIM(RTRIM(r.BranchName)) LIKE N'%-' + LTRIM(RTRIM(@BranchCode))
                OR LTRIM(RTRIM(r.BranchName)) LIKE N'%' + LTRIM(RTRIM(@BranchCode)) + N'%'
              )
          AND (
                @Creator IS NULL
                OR EXISTS
                (
                    SELECT 1
                    FROM dbo.UserCreatorMapping_VisitReport c
                    WHERE ISNULL(c.IsActive, 1) = 1
                      AND LTRIM(RTRIM(c.EmpCode)) = LTRIM(RTRIM(r.EmployeeCode))
                      AND LTRIM(RTRIM(c.Creator)) LIKE N'%' + LTRIM(RTRIM(@Creator)) + N'%'
                      AND (
                            LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(c.Code))
                            OR LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(c.Creator)) + N'-' + LTRIM(RTRIM(ISNULL(c.Code, N'')))
                            OR (
                                  NULLIF(LTRIM(RTRIM(c.Code)), N'') IS NOT NULL
                                  AND LTRIM(RTRIM(r.BranchName)) LIKE N'%-' + LTRIM(RTRIM(c.Code))
                               )
                            OR LTRIM(RTRIM(r.BranchName)) LIKE LTRIM(RTRIM(c.Creator)) + N'%'
                          )
                )
              )
        GROUP BY LTRIM(RTRIM(r.EmployeeCode))
    )
    SELECT
        EmpCode,
        ISNULL(Name, EmpCode) AS Name,
        VisitCount
    FROM Filtered
    ORDER BY VisitCount ASC, Name ASC
    OFFSET (@Page - 1) * @PageSize ROWS
    FETCH NEXT @PageSize ROWS ONLY;

    ;WITH Filtered AS
    (
        SELECT LTRIM(RTRIM(r.EmployeeCode)) AS EmpCode
        FROM dbo.LendingVisitReports r
        OUTER APPLY
        (
            SELECT TOP (1) um.EmpType
            FROM dbo.UserReportingMapping_VisitReport um
            WHERE ISNULL(um.IsActive, 1) = 1
              AND LTRIM(RTRIM(um.EmpCode)) = LTRIM(RTRIM(r.EmployeeCode))
            ORDER BY um.Id DESC
        ) map
        WHERE NULLIF(LTRIM(RTRIM(r.EmployeeCode)), N'') IS NOT NULL
          AND (@FromDate IS NULL OR r.VisitDateTime >= @FromDate)
          AND (@ToDate   IS NULL OR r.VisitDateTime <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
          AND (
                @EmpType IS NULL
                OR UPPER(LTRIM(RTRIM(ISNULL(r.FormRole, map.EmpType)))) = @EmpType
              )
          AND (
                @EmployeeCode IS NULL
                OR LTRIM(RTRIM(r.EmployeeCode)) = LTRIM(RTRIM(@EmployeeCode))
              )
          AND (
                @BranchCode IS NULL
                OR LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(@BranchCode))
                OR LTRIM(RTRIM(r.BranchName)) LIKE N'%-' + LTRIM(RTRIM(@BranchCode))
                OR LTRIM(RTRIM(r.BranchName)) LIKE N'%' + LTRIM(RTRIM(@BranchCode)) + N'%'
              )
          AND (
                @Creator IS NULL
                OR EXISTS
                (
                    SELECT 1
                    FROM dbo.UserCreatorMapping_VisitReport c
                    WHERE ISNULL(c.IsActive, 1) = 1
                      AND LTRIM(RTRIM(c.EmpCode)) = LTRIM(RTRIM(r.EmployeeCode))
                      AND LTRIM(RTRIM(c.Creator)) LIKE N'%' + LTRIM(RTRIM(@Creator)) + N'%'
                      AND (
                            LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(c.Code))
                            OR LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(c.Creator)) + N'-' + LTRIM(RTRIM(ISNULL(c.Code, N'')))
                            OR (
                                  NULLIF(LTRIM(RTRIM(c.Code)), N'') IS NOT NULL
                                  AND LTRIM(RTRIM(r.BranchName)) LIKE N'%-' + LTRIM(RTRIM(c.Code))
                               )
                            OR LTRIM(RTRIM(r.BranchName)) LIKE LTRIM(RTRIM(c.Creator)) + N'%'
                          )
                )
              )
        GROUP BY LTRIM(RTRIM(r.EmployeeCode))
    )
    SELECT COUNT(1) AS TotalCount FROM Filtered;
END
GO
