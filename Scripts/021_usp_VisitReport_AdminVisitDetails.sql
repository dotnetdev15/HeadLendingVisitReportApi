/* =====================================================================
   Admin Visit Report — Visit Count popup details
   Returns Branch Code / Branch Name / Creator for one EmpCode in date range
   ===================================================================== */
CREATE OR ALTER PROCEDURE dbo.usp_VisitReport_AdminVisitDetails
(
    @FromDate     DATETIME2      = NULL,
    @ToDate       DATETIME2      = NULL,
    @EmployeeCode NVARCHAR(50),
    @EmpType      NVARCHAR(50)   = NULL,
    @Creator      NVARCHAR(100)  = NULL,
    @BranchCode   NVARCHAR(50)   = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF LTRIM(RTRIM(ISNULL(@EmployeeCode, N''))) = N''
        RETURN;

    SET @EmployeeCode = LTRIM(RTRIM(@EmployeeCode));

    IF LTRIM(RTRIM(ISNULL(@EmpType,    N''))) = N'' SET @EmpType    = NULL;
    IF LTRIM(RTRIM(ISNULL(@Creator,    N''))) = N'' SET @Creator    = NULL;
    IF LTRIM(RTRIM(ISNULL(@BranchCode, N''))) = N'' SET @BranchCode = NULL;

    IF @EmpType IS NOT NULL
        SET @EmpType = UPPER(LTRIM(RTRIM(@EmpType)));

    SELECT
        r.Id AS VisitReportId,
        CASE
            WHEN CHARINDEX(N'-', LTRIM(RTRIM(ISNULL(r.BranchName, N'')))) > 0
            THEN LTRIM(RTRIM(RIGHT(
                    LTRIM(RTRIM(r.BranchName)),
                    LEN(LTRIM(RTRIM(r.BranchName))) - CHARINDEX(N'-', LTRIM(RTRIM(r.BranchName)))
                 )))
            ELSE LTRIM(RTRIM(ISNULL(c.Code, r.BranchName)))
        END AS BranchCode,
        CASE
            WHEN NULLIF(LTRIM(RTRIM(c.Branch)), N'') IS NOT NULL
            THEN LTRIM(RTRIM(c.Branch))
            WHEN CHARINDEX(N'-', LTRIM(RTRIM(ISNULL(r.BranchName, N'')))) > 0
            THEN LTRIM(RTRIM(LEFT(
                    LTRIM(RTRIM(r.BranchName)),
                    CHARINDEX(N'-', LTRIM(RTRIM(r.BranchName))) - 1
                 )))
            ELSE LTRIM(RTRIM(ISNULL(r.BranchName, N'')))
        END AS BranchName,
        ISNULL(LTRIM(RTRIM(c.Creator)), N'') AS Creator,
        r.VisitDateTime,
        r.HeadName AS Name,
        r.EmployeeCode AS EmpCode
    FROM dbo.LendingVisitReports r
    OUTER APPLY
    (
        SELECT TOP (1)
            cm.Creator,
            cm.Branch,
            cm.Code
        FROM dbo.UserCreatorMapping_VisitReport cm
        WHERE ISNULL(cm.IsActive, 1) = 1
          AND LTRIM(RTRIM(cm.EmpCode)) = LTRIM(RTRIM(r.EmployeeCode))
          AND (
                LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(cm.Code))
                OR LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(cm.Creator)) + N'-' + LTRIM(RTRIM(ISNULL(cm.Code, N'')))
                OR (
                      NULLIF(LTRIM(RTRIM(cm.Code)), N'') IS NOT NULL
                      AND LTRIM(RTRIM(r.BranchName)) LIKE N'%-' + LTRIM(RTRIM(cm.Code))
                   )
                OR LTRIM(RTRIM(r.BranchName)) LIKE LTRIM(RTRIM(cm.Creator)) + N'%'
              )
        ORDER BY cm.Id DESC
    ) c
    OUTER APPLY
    (
        SELECT TOP (1) um.EmpType
        FROM dbo.UserReportingMapping_VisitReport um
        WHERE ISNULL(um.IsActive, 1) = 1
          AND LTRIM(RTRIM(um.EmpCode)) = LTRIM(RTRIM(r.EmployeeCode))
        ORDER BY um.Id DESC
    ) map
    WHERE LTRIM(RTRIM(r.EmployeeCode)) = @EmployeeCode
      AND (@FromDate IS NULL OR r.VisitDateTime >= @FromDate)
      AND (@ToDate   IS NULL OR r.VisitDateTime <  DATEADD(DAY, 1, CAST(@ToDate AS DATE)))
      AND (
            @EmpType IS NULL
            OR UPPER(LTRIM(RTRIM(ISNULL(r.FormRole, map.EmpType)))) = @EmpType
          )
      AND (
            @BranchCode IS NULL
            OR LTRIM(RTRIM(r.BranchName)) = LTRIM(RTRIM(@BranchCode))
            OR LTRIM(RTRIM(r.BranchName)) LIKE N'%-' + LTRIM(RTRIM(@BranchCode))
            OR LTRIM(RTRIM(ISNULL(c.Code, N''))) = LTRIM(RTRIM(@BranchCode))
            OR LTRIM(RTRIM(r.BranchName)) LIKE N'%' + LTRIM(RTRIM(@BranchCode)) + N'%'
          )
      AND (
            @Creator IS NULL
            OR LTRIM(RTRIM(ISNULL(c.Creator, N''))) LIKE N'%' + LTRIM(RTRIM(@Creator)) + N'%'
          )
    ORDER BY r.VisitDateTime DESC, r.Id DESC;
END
GO
