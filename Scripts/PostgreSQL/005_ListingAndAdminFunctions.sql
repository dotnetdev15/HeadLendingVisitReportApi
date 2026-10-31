/* =====================================================================
   Listing / role-team / admin / login-profile / mapping functions
   PostgreSQL 10.3 port of 018 (GetAll), 017 (GetByRoleTeam),
   020 (AdminVisitSummary), 021 (AdminVisitDetails), 016 (LoginProfile)
   and the Creator/Reporting mapping procedures.

   Every SQL Server procedure that returned "page rows" + "TotalCount"
   as two result sets is now a pair of functions (<name> and
   <name>_Count) sharing one *_Filtered function, so the filter logic
   lives in one place. The API batches both calls in one command, so
   reader.NextResultAsync() works exactly as before.

   SQL Server's default collation is case-insensitive, so LIKE became
   ILIKE and e-mail comparisons use LOWER() to keep the same matches.
   ===================================================================== */

/* =====================================================================
   usp_VisitReport_GetAll (018) — viewer's own reports
   ===================================================================== */
CREATE OR REPLACE FUNCTION dbo.fn_VisitReport_GetAll_Filtered
(
    p_BranchName      VARCHAR,
    p_EmployeeCode    VARCHAR,
    p_FromDate        TIMESTAMP,
    p_ToDate          TIMESTAMP,
    p_Email           VARCHAR,
    p_SelectedEmpCode VARCHAR,
    p_ViewRole        VARCHAR
)
RETURNS SETOF dbo.LendingVisitReports
LANGUAGE sql STABLE
AS $$
    WITH f AS
    (
        SELECT
            NULLIF(TRIM(p_BranchName), '')      AS BranchName,
            NULLIF(TRIM(p_EmployeeCode), '')    AS EmployeeCode,
            NULLIF(TRIM(p_Email), '')           AS Email,
            NULLIF(TRIM(p_SelectedEmpCode), '') AS SelectedEmpCode
    ),
    v AS
    (
        SELECT
            f.*,
            (
                SELECT COALESCE(NULLIF(TRIM(um.EmpCode), ''), um.OldEmpCode)::VARCHAR(50)
                FROM dbo.UsersMasters um
                WHERE f.Email IS NOT NULL
                  AND LOWER(um.Email) = LOWER(f.Email)
                  AND COALESCE(um.IsActive, TRUE)   = TRUE
                  AND COALESCE(um.IsDeleted, FALSE) = FALSE
                LIMIT 1
            ) AS ViewerEmp
        FROM f
    )
    SELECT r.*
    FROM dbo.LendingVisitReports r
    CROSS JOIN v
    WHERE (v.BranchName      IS NULL OR r.BranchName ILIKE '%' || v.BranchName || '%')
      AND (v.EmployeeCode    IS NULL OR r.EmployeeCode = v.EmployeeCode)
      AND (p_FromDate        IS NULL OR r.VisitDateTime >= p_FromDate)
      AND (p_ToDate          IS NULL OR r.VisitDateTime <  (CAST(p_ToDate AS DATE) + 1))
      AND (v.SelectedEmpCode IS NULL OR r.EmployeeCode = v.SelectedEmpCode)
      AND (v.Email           IS NULL OR (v.ViewerEmp IS NOT NULL AND r.EmployeeCode = v.ViewerEmp));
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetAll
(
    p_Page            INT,
    p_PageSize        INT,
    p_BranchName      VARCHAR,
    p_EmployeeCode    VARCHAR,
    p_FromDate        TIMESTAMP,
    p_ToDate          TIMESTAMP,
    p_Email           VARCHAR,
    p_SelectedEmpCode VARCHAR,
    p_ViewRole        VARCHAR
)
RETURNS SETOF dbo.LendingVisitReports
LANGUAGE sql STABLE
AS $$
    SELECT *
    FROM dbo.fn_VisitReport_GetAll_Filtered(p_BranchName, p_EmployeeCode, p_FromDate, p_ToDate, p_Email, p_SelectedEmpCode, p_ViewRole)
    ORDER BY CreatedAtUtc DESC
    OFFSET (GREATEST(COALESCE(p_Page, 1), 1) - 1) * (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 20 ELSE p_PageSize END)
    LIMIT  (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 20 ELSE p_PageSize END);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetAll_Count
(
    p_BranchName      VARCHAR,
    p_EmployeeCode    VARCHAR,
    p_FromDate        TIMESTAMP,
    p_ToDate          TIMESTAMP,
    p_Email           VARCHAR,
    p_SelectedEmpCode VARCHAR,
    p_ViewRole        VARCHAR
)
RETURNS TABLE (TotalCount INT)
LANGUAGE sql STABLE
AS $$
    SELECT COUNT(*)::INT
    FROM dbo.fn_VisitReport_GetAll_Filtered(p_BranchName, p_EmployeeCode, p_FromDate, p_ToDate, p_Email, p_SelectedEmpCode, p_ViewRole);
$$;

/* =====================================================================
   usp_VisitReport_GetByRoleTeam (017) — reports of the viewer's team,
   walking UserReportingMappingMasters down to 10 levels.
   The viewer is resolved from @Email; when no e-mail is sent the
   @EmpCode the API passes is used directly. @Search matches branch,
   head name or employee code.
   ===================================================================== */
CREATE OR REPLACE FUNCTION dbo.fn_VisitReport_GetByRoleTeam_Filtered
(
    p_BranchName   VARCHAR,
    p_EmployeeCode VARCHAR,
    p_FromDate     TIMESTAMP,
    p_ToDate       TIMESTAMP,
    p_Email        VARCHAR,
    p_EmpCode      VARCHAR,
    p_EmpType      VARCHAR,
    p_Search       VARCHAR,
    p_RoleName     VARCHAR
)
RETURNS SETOF dbo.LendingVisitReports
LANGUAGE sql STABLE
AS $$
    WITH RECURSIVE f AS
    (
        SELECT
            NULLIF(TRIM(p_BranchName), '')   AS BranchName,
            NULLIF(TRIM(p_EmployeeCode), '') AS EmployeeCode,
            NULLIF(TRIM(p_Search), '')       AS Search,
            COALESCE(NULLIF(TRIM(p_EmpType), ''), NULLIF(TRIM(p_RoleName), '')) AS EmpType,
            COALESCE(
                (
                    SELECT COALESCE(NULLIF(TRIM(um.EmpCode), ''), um.OldEmpCode)::VARCHAR(50)
                    FROM dbo.UsersMasters um
                    WHERE NULLIF(TRIM(p_Email), '') IS NOT NULL
                      AND LOWER(um.Email) = LOWER(TRIM(p_Email))
                      AND COALESCE(um.IsActive, TRUE)   = TRUE
                      AND COALESCE(um.IsDeleted, FALSE) = FALSE
                    LIMIT 1
                ),
                NULLIF(TRIM(p_EmpCode), '')
            ) AS ViewerEmp
    ),
    Tree AS
    (
        SELECT
            m.Id,
            TRIM(m.EmpCode)::VARCHAR       AS EmpCode,
            TRIM(m.OldEmpCode)::VARCHAR    AS OldEmpCode,
            TRIM(m.ParentEmpCode)::VARCHAR AS ParentEmpCode,
            TRIM(m.EmpType)::VARCHAR       AS EmpType,
            0 AS Lvl
        FROM dbo.UserReportingMappingMasters m
        CROSS JOIN f
        WHERE COALESCE(m.IsActive, TRUE) = TRUE
          AND f.ViewerEmp IS NOT NULL
          AND (TRIM(m.EmpCode) = f.ViewerEmp OR TRIM(m.OldEmpCode) = f.ViewerEmp)

        UNION ALL

        SELECT
            c.Id,
            TRIM(c.EmpCode)::VARCHAR,
            TRIM(c.OldEmpCode)::VARCHAR,
            TRIM(c.ParentEmpCode)::VARCHAR,
            TRIM(c.EmpType)::VARCHAR,
            t.Lvl + 1
        FROM dbo.UserReportingMappingMasters c
        INNER JOIN Tree t
            ON COALESCE(c.IsActive, TRUE) = TRUE
           AND t.Lvl < 10
           AND (
                (t.EmpCode    IS NOT NULL AND TRIM(c.ParentEmpCode) = t.EmpCode)
             OR (t.OldEmpCode IS NOT NULL AND TRIM(c.ParentEmpCode) = t.OldEmpCode)
               )
    ),
    RoleEmps AS
    (
        SELECT DISTINCT v.Code
        FROM Tree t
        CROSS JOIN f
        CROSS JOIN LATERAL (VALUES (t.EmpCode), (t.OldEmpCode)) AS v(Code)
        WHERE t.Lvl > 0
          AND v.Code IS NOT NULL
          AND v.Code <> ''
          AND (f.EmpType IS NULL OR UPPER(t.EmpType) = UPPER(f.EmpType))
    )
    SELECT r.*
    FROM dbo.LendingVisitReports r
    CROSS JOIN f
    WHERE (f.BranchName   IS NULL OR r.BranchName ILIKE '%' || f.BranchName || '%')
      AND (f.EmployeeCode IS NULL OR r.EmployeeCode = f.EmployeeCode)
      AND (p_FromDate     IS NULL OR r.VisitDateTime >= p_FromDate)
      AND (p_ToDate       IS NULL OR r.VisitDateTime <  (CAST(p_ToDate AS DATE) + 1))
      AND (f.Search       IS NULL
           OR r.BranchName   ILIKE '%' || f.Search || '%'
           OR r.HeadName     ILIKE '%' || f.Search || '%'
           OR r.EmployeeCode ILIKE '%' || f.Search || '%')
      AND f.ViewerEmp IS NOT NULL
      AND r.EmployeeCode IN (SELECT Code FROM RoleEmps);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetByRoleTeam
(
    p_Page         INT,
    p_PageSize     INT,
    p_BranchName   VARCHAR,
    p_EmployeeCode VARCHAR,
    p_FromDate     TIMESTAMP,
    p_ToDate       TIMESTAMP,
    p_Email        VARCHAR,
    p_EmpCode      VARCHAR,
    p_EmpType      VARCHAR,
    p_Search       VARCHAR,
    p_RoleName     VARCHAR
)
RETURNS SETOF dbo.LendingVisitReports
LANGUAGE sql STABLE
AS $$
    SELECT *
    FROM dbo.fn_VisitReport_GetByRoleTeam_Filtered(p_BranchName, p_EmployeeCode, p_FromDate, p_ToDate, p_Email, p_EmpCode, p_EmpType, p_Search, p_RoleName)
    ORDER BY CreatedAtUtc DESC
    OFFSET (GREATEST(COALESCE(p_Page, 1), 1) - 1) * (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 20 ELSE p_PageSize END)
    LIMIT  (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 20 ELSE p_PageSize END);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetByRoleTeam_Count
(
    p_BranchName   VARCHAR,
    p_EmployeeCode VARCHAR,
    p_FromDate     TIMESTAMP,
    p_ToDate       TIMESTAMP,
    p_Email        VARCHAR,
    p_EmpCode      VARCHAR,
    p_EmpType      VARCHAR,
    p_Search       VARCHAR,
    p_RoleName     VARCHAR
)
RETURNS TABLE (TotalCount INT)
LANGUAGE sql STABLE
AS $$
    SELECT COUNT(*)::INT
    FROM dbo.fn_VisitReport_GetByRoleTeam_Filtered(p_BranchName, p_EmployeeCode, p_FromDate, p_ToDate, p_Email, p_EmpCode, p_EmpType, p_Search, p_RoleName);
$$;

/* =====================================================================
   usp_VisitReport_AdminVisitSummary (020)
   ===================================================================== */
CREATE OR REPLACE FUNCTION dbo.fn_VisitReport_AdminVisitSummary_Filtered
(
    p_FromDate     TIMESTAMP,
    p_ToDate       TIMESTAMP,
    p_EmpType      VARCHAR,
    p_Creator      VARCHAR,
    p_BranchCode   VARCHAR,
    p_EmployeeCode VARCHAR
)
RETURNS TABLE (EmpCode VARCHAR, Name VARCHAR, VisitCount INT)
LANGUAGE sql STABLE
AS $$
    WITH f AS
    (
        SELECT
            UPPER(NULLIF(TRIM(p_EmpType), ''))  AS EmpType,
            NULLIF(TRIM(p_Creator), '')         AS Creator,
            NULLIF(TRIM(p_BranchCode), '')      AS BranchCode,
            NULLIF(TRIM(p_EmployeeCode), '')    AS EmployeeCode
    )
    SELECT
        TRIM(r.EmployeeCode)::VARCHAR                          AS EmpCode,
        MAX(NULLIF(TRIM(r.HeadName), ''))::VARCHAR             AS Name,
        COUNT(1)::INT                                          AS VisitCount
    FROM dbo.LendingVisitReports r
    CROSS JOIN f
    LEFT JOIN LATERAL
    (
        SELECT um.EmpType
        FROM dbo.UserReportingMapping_VisitReport um
        WHERE COALESCE(um.IsActive, TRUE) = TRUE
          AND TRIM(um.EmpCode) = TRIM(r.EmployeeCode)
        ORDER BY um.Id DESC
        LIMIT 1
    ) map ON TRUE
    WHERE NULLIF(TRIM(r.EmployeeCode), '') IS NOT NULL
      AND (p_FromDate IS NULL OR r.VisitDateTime >= p_FromDate)
      AND (p_ToDate   IS NULL OR r.VisitDateTime <  (CAST(p_ToDate AS DATE) + 1))
      AND (f.EmpType IS NULL OR UPPER(TRIM(COALESCE(r.FormRole, map.EmpType))) = f.EmpType)
      AND (f.EmployeeCode IS NULL OR TRIM(r.EmployeeCode) = f.EmployeeCode)
      AND (
            f.BranchCode IS NULL
            OR TRIM(r.BranchName) ILIKE f.BranchCode
            OR TRIM(r.BranchName) ILIKE '%-' || f.BranchCode
            OR TRIM(r.BranchName) ILIKE '%' || f.BranchCode || '%'
          )
      AND (
            f.Creator IS NULL
            OR EXISTS
            (
                SELECT 1
                FROM dbo.UserCreatorMapping_VisitReport c
                WHERE COALESCE(c.IsActive, TRUE) = TRUE
                  AND TRIM(c.EmpCode) = TRIM(r.EmployeeCode)
                  AND TRIM(c.Creator) ILIKE '%' || f.Creator || '%'
                  AND (
                        TRIM(r.BranchName) = TRIM(c.Code)
                        OR TRIM(r.BranchName) = TRIM(c.Creator) || '-' || TRIM(COALESCE(c.Code, ''))
                        OR (NULLIF(TRIM(c.Code), '') IS NOT NULL AND TRIM(r.BranchName) ILIKE '%-' || TRIM(c.Code))
                        OR TRIM(r.BranchName) ILIKE TRIM(c.Creator) || '%'
                      )
            )
          )
    GROUP BY TRIM(r.EmployeeCode);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_AdminVisitSummary
(
    p_Page         INT,
    p_PageSize     INT,
    p_FromDate     TIMESTAMP,
    p_ToDate       TIMESTAMP,
    p_EmpType      VARCHAR,
    p_Creator      VARCHAR,
    p_BranchCode   VARCHAR,
    p_EmployeeCode VARCHAR
)
RETURNS TABLE (EmpCode VARCHAR, Name VARCHAR, VisitCount INT)
LANGUAGE sql STABLE
AS $$
    SELECT s.EmpCode, COALESCE(s.Name, s.EmpCode)::VARCHAR AS Name, s.VisitCount
    FROM dbo.fn_VisitReport_AdminVisitSummary_Filtered(p_FromDate, p_ToDate, p_EmpType, p_Creator, p_BranchCode, p_EmployeeCode) s
    ORDER BY s.VisitCount ASC, COALESCE(s.Name, s.EmpCode) ASC
    OFFSET (GREATEST(COALESCE(p_Page, 1), 1) - 1) * (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 10 ELSE p_PageSize END)
    LIMIT  (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 10 ELSE p_PageSize END);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_AdminVisitSummary_Count
(
    p_FromDate     TIMESTAMP,
    p_ToDate       TIMESTAMP,
    p_EmpType      VARCHAR,
    p_Creator      VARCHAR,
    p_BranchCode   VARCHAR,
    p_EmployeeCode VARCHAR
)
RETURNS TABLE (TotalCount INT)
LANGUAGE sql STABLE
AS $$
    SELECT COUNT(1)::INT
    FROM dbo.fn_VisitReport_AdminVisitSummary_Filtered(p_FromDate, p_ToDate, p_EmpType, p_Creator, p_BranchCode, p_EmployeeCode);
$$;

/* =====================================================================
   usp_VisitReport_AdminVisitDetails (021)
   ===================================================================== */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_AdminVisitDetails
(
    p_FromDate     TIMESTAMP,
    p_ToDate       TIMESTAMP,
    p_EmployeeCode VARCHAR,
    p_EmpType      VARCHAR,
    p_Creator      VARCHAR,
    p_BranchCode   VARCHAR
)
RETURNS TABLE
(
    VisitReportId INT,
    BranchCode    VARCHAR,
    BranchName    VARCHAR,
    Creator       VARCHAR,
    VisitDateTime TIMESTAMP,
    Name          VARCHAR,
    EmpCode       VARCHAR
)
LANGUAGE sql STABLE
AS $$
    WITH f AS
    (
        SELECT
            NULLIF(TRIM(p_EmployeeCode), '')   AS EmployeeCode,
            UPPER(NULLIF(TRIM(p_EmpType), '')) AS EmpType,
            NULLIF(TRIM(p_Creator), '')        AS Creator,
            NULLIF(TRIM(p_BranchCode), '')     AS BranchCode
    )
    SELECT
        r.Id AS VisitReportId,
        (CASE
            WHEN strpos(TRIM(COALESCE(r.BranchName, '')), '-') > 0
            THEN TRIM(substr(TRIM(r.BranchName), strpos(TRIM(r.BranchName), '-') + 1))
            ELSE TRIM(COALESCE(c.Code, r.BranchName))
        END)::VARCHAR AS BranchCode,
        (CASE
            WHEN NULLIF(TRIM(c.Branch), '') IS NOT NULL
            THEN TRIM(c.Branch)
            WHEN strpos(TRIM(COALESCE(r.BranchName, '')), '-') > 0
            THEN TRIM(substr(TRIM(r.BranchName), 1, strpos(TRIM(r.BranchName), '-') - 1))
            ELSE TRIM(COALESCE(r.BranchName, ''))
        END)::VARCHAR AS BranchName,
        COALESCE(TRIM(c.Creator), '')::VARCHAR AS Creator,
        r.VisitDateTime,
        r.HeadName::VARCHAR     AS Name,
        r.EmployeeCode::VARCHAR AS EmpCode
    FROM dbo.LendingVisitReports r
    CROSS JOIN f
    LEFT JOIN LATERAL
    (
        SELECT cm.Creator, cm.Branch, cm.Code
        FROM dbo.UserCreatorMapping_VisitReport cm
        WHERE COALESCE(cm.IsActive, TRUE) = TRUE
          AND TRIM(cm.EmpCode) = TRIM(r.EmployeeCode)
          AND (
                TRIM(r.BranchName) = TRIM(cm.Code)
                OR TRIM(r.BranchName) = TRIM(cm.Creator) || '-' || TRIM(COALESCE(cm.Code, ''))
                OR (NULLIF(TRIM(cm.Code), '') IS NOT NULL AND TRIM(r.BranchName) ILIKE '%-' || TRIM(cm.Code))
                OR TRIM(r.BranchName) ILIKE TRIM(cm.Creator) || '%'
              )
        ORDER BY cm.Id DESC
        LIMIT 1
    ) c ON TRUE
    LEFT JOIN LATERAL
    (
        SELECT um.EmpType
        FROM dbo.UserReportingMapping_VisitReport um
        WHERE COALESCE(um.IsActive, TRUE) = TRUE
          AND TRIM(um.EmpCode) = TRIM(r.EmployeeCode)
        ORDER BY um.Id DESC
        LIMIT 1
    ) map ON TRUE
    WHERE f.EmployeeCode IS NOT NULL                       -- original: RETURN when blank
      AND TRIM(r.EmployeeCode) = f.EmployeeCode
      AND (p_FromDate IS NULL OR r.VisitDateTime >= p_FromDate)
      AND (p_ToDate   IS NULL OR r.VisitDateTime <  (CAST(p_ToDate AS DATE) + 1))
      AND (f.EmpType IS NULL OR UPPER(TRIM(COALESCE(r.FormRole, map.EmpType))) = f.EmpType)
      AND (
            f.BranchCode IS NULL
            OR TRIM(r.BranchName) ILIKE f.BranchCode
            OR TRIM(r.BranchName) ILIKE '%-' || f.BranchCode
            OR TRIM(COALESCE(c.Code, '')) ILIKE f.BranchCode
            OR TRIM(r.BranchName) ILIKE '%' || f.BranchCode || '%'
          )
      AND (f.Creator IS NULL OR TRIM(COALESCE(c.Creator, '')) ILIKE '%' || f.Creator || '%')
    ORDER BY r.VisitDateTime DESC, r.Id DESC;
$$;

/* =====================================================================
   usp_VisitReport_GetLoginProfile — called by the API with @EmpCode.
   Result 1: profile row.  Result 2 (usp_VisitReport_GetLoginBranches):
   the "Creator-Code" branch list from the creator mapping.
   ===================================================================== */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetLoginProfile(p_EmpCode VARCHAR)
RETURNS TABLE
(
    Id            INT,
    Email         VARCHAR,
    Name          VARCHAR,
    RoleId        INT,
    RoleName      VARCHAR,
    FormRole      VARCHAR,
    EmpType       VARCHAR,
    BranchCode    VARCHAR,
    BranchName    VARCHAR,
    Creator       VARCHAR,
    EmpCode       VARCHAR,
    ParentEmpCode VARCHAR
)
LANGUAGE sql STABLE
AS $$
    WITH u AS
    (
        SELECT um.*,
               COALESCE(NULLIF(TRIM(um.EmpCode), ''), um.OldEmpCode)::VARCHAR(50) AS ResolvedEmpCode
        FROM dbo.UsersMasters um
        WHERE COALESCE(um.IsActive, TRUE)   = TRUE
          AND COALESCE(um.IsDeleted, FALSE) = FALSE
          AND (
                COALESCE(NULLIF(TRIM(um.EmpCode), ''), um.OldEmpCode) = TRIM(p_EmpCode)
                OR TRIM(um.OldEmpCode) = TRIM(p_EmpCode)
              )
        LIMIT 1
    )
    SELECT
        u.Id,
        u.Email::VARCHAR,
        u.Name::VARCHAR,
        CAST(NULL AS INT)          AS RoleId,
        m.EmpType::VARCHAR         AS RoleName,
        m.EmpType::VARCHAR         AS FormRole,
        m.EmpType::VARCHAR         AS EmpType,
        u.BranchCode::VARCHAR,
        CAST(NULL AS VARCHAR(200)) AS BranchName,
        cm.Creator::VARCHAR        AS Creator,
        u.ResolvedEmpCode::VARCHAR AS EmpCode,
        m.ParentEmpCode::VARCHAR
    FROM u
    LEFT JOIN LATERAL
    (
        SELECT mm.EmpType, mm.ParentEmpCode
        FROM dbo.UserReportingMappingMasters mm
        WHERE COALESCE(mm.IsActive, TRUE) = TRUE
          AND (TRIM(mm.EmpCode) = u.ResolvedEmpCode OR TRIM(mm.OldEmpCode) = u.ResolvedEmpCode)
        ORDER BY mm.Id DESC
        LIMIT 1
    ) m ON TRUE
    LEFT JOIN LATERAL
    (
        SELECT TRIM(c.Creator) AS Creator
        FROM dbo.UserCreatorMapping_VisitReport c
        WHERE COALESCE(c.IsActive, TRUE) = TRUE
          AND TRIM(c.EmpCode) = u.ResolvedEmpCode
        ORDER BY c.Id DESC
        LIMIT 1
    ) cm ON TRUE;
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetLoginBranches(p_EmpCode VARCHAR)
RETURNS TABLE (BranchName VARCHAR)
LANGUAGE sql STABLE
AS $$
    SELECT DISTINCT
        (CASE
            WHEN NULLIF(TRIM(c.Code), '') IS NOT NULL
            THEN TRIM(c.Creator) || '-' || TRIM(c.Code)
            ELSE TRIM(c.Creator)
        END)::VARCHAR AS BranchName
    FROM dbo.UserCreatorMapping_VisitReport c
    WHERE COALESCE(c.IsActive, TRUE) = TRUE
      AND TRIM(c.EmpCode) = TRIM(p_EmpCode)
      AND NULLIF(TRIM(c.Creator), '') IS NOT NULL;
$$;

/* =====================================================================
   Creator mapping (UserCreatorMapping_VisitReport)
   ===================================================================== */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_CreatorMapping_List(p_Page INT, p_PageSize INT, p_EmpCode VARCHAR)
RETURNS TABLE (Id INT, EmpCode VARCHAR, Creator VARCHAR, BranchName VARCHAR, BranchCode VARCHAR, IsActive BOOLEAN)
LANGUAGE sql STABLE
AS $$
    SELECT c.Id, c.EmpCode::VARCHAR, c.Creator::VARCHAR, c.Branch::VARCHAR, c.Code::VARCHAR, COALESCE(c.IsActive, TRUE)
    FROM dbo.UserCreatorMapping_VisitReport c
    WHERE NULLIF(TRIM(p_EmpCode), '') IS NULL OR TRIM(c.EmpCode) = TRIM(p_EmpCode)
    ORDER BY c.Id DESC
    OFFSET (GREATEST(COALESCE(p_Page, 1), 1) - 1) * (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 10 ELSE p_PageSize END)
    LIMIT  (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 10 ELSE p_PageSize END);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_CreatorMapping_Count(p_EmpCode VARCHAR)
RETURNS TABLE (TotalCount INT)
LANGUAGE sql STABLE
AS $$
    SELECT COUNT(*)::INT
    FROM dbo.UserCreatorMapping_VisitReport c
    WHERE NULLIF(TRIM(p_EmpCode), '') IS NULL OR TRIM(c.EmpCode) = TRIM(p_EmpCode);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_CreatorMapping_UpdateStatus(p_Id INT, p_IsActive BOOLEAN)
RETURNS TABLE (RowsAffected INT)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
DECLARE v_Count INT := 0;
BEGIN
    UPDATE dbo.UserCreatorMapping_VisitReport SET IsActive = p_IsActive WHERE Id = p_Id;
    GET DIAGNOSTICS v_Count = ROW_COUNT;
    RETURN QUERY SELECT v_Count;
END;
$$;

/* Rows = JSONB array of {"EmpCode","Creator","BranchName","BranchCode"}
   (replaces dbo.VisitReportCreatorMappingUploadType). Rows that already
   exist as an active mapping (same EmpCode + Creator + Code), or repeat
   within the upload, are skipped. */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_CreatorMapping_BulkUpload(p_Rows JSONB, p_CreatedBy VARCHAR)
RETURNS TABLE (InsertedCount INT, SkippedCount INT)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
DECLARE
    v_Total    INT := 0;
    v_Inserted INT := 0;
BEGIN
    SELECT COUNT(*) INTO v_Total
    FROM jsonb_to_recordset(COALESCE(p_Rows, '[]'::jsonb)) AS x("EmpCode" VARCHAR)
    WHERE NULLIF(TRIM(x."EmpCode"), '') IS NOT NULL;

    INSERT INTO dbo.UserCreatorMapping_VisitReport (EmpCode, Creator, Branch, Code, IsActive, CreatedBy)
    SELECT DISTINCT ON (TRIM(x."EmpCode"), COALESCE(TRIM(x."Creator"), ''), COALESCE(TRIM(x."BranchCode"), ''))
           TRIM(x."EmpCode"), TRIM(x."Creator"), TRIM(x."BranchName"), TRIM(x."BranchCode"), TRUE, NULLIF(TRIM(p_CreatedBy), '')
    FROM jsonb_to_recordset(COALESCE(p_Rows, '[]'::jsonb))
         AS x("EmpCode" VARCHAR, "Creator" VARCHAR, "BranchName" VARCHAR, "BranchCode" VARCHAR)
    WHERE NULLIF(TRIM(x."EmpCode"), '') IS NOT NULL
      AND NOT EXISTS
      (
          SELECT 1 FROM dbo.UserCreatorMapping_VisitReport c
          WHERE COALESCE(c.IsActive, TRUE) = TRUE
            AND TRIM(c.EmpCode) = TRIM(x."EmpCode")
            AND COALESCE(TRIM(c.Creator), '') = COALESCE(TRIM(x."Creator"), '')
            AND COALESCE(TRIM(c.Code), '')    = COALESCE(TRIM(x."BranchCode"), '')
      );

    GET DIAGNOSTICS v_Inserted = ROW_COUNT;
    RETURN QUERY SELECT v_Inserted, GREATEST(v_Total - v_Inserted, 0);
END;
$$;

/* =====================================================================
   Reporting mapping (UserReportingMapping_VisitReport)
   ===================================================================== */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_ReportingMapping_List(p_Page INT, p_PageSize INT, p_EmpCode VARCHAR)
RETURNS TABLE (Id INT, EmpCode VARCHAR, ParentEmpCode VARCHAR, EmpType VARCHAR, IsActive BOOLEAN)
LANGUAGE sql STABLE
AS $$
    SELECT m.Id, m.EmpCode::VARCHAR, m.ParentEmpCode::VARCHAR, m.EmpType::VARCHAR, COALESCE(m.IsActive, TRUE)
    FROM dbo.UserReportingMapping_VisitReport m
    WHERE NULLIF(TRIM(p_EmpCode), '') IS NULL OR TRIM(m.EmpCode) = TRIM(p_EmpCode)
    ORDER BY m.Id DESC
    OFFSET (GREATEST(COALESCE(p_Page, 1), 1) - 1) * (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 10 ELSE p_PageSize END)
    LIMIT  (CASE WHEN COALESCE(p_PageSize, 0) < 1 THEN 10 ELSE p_PageSize END);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_ReportingMapping_Count(p_EmpCode VARCHAR)
RETURNS TABLE (TotalCount INT)
LANGUAGE sql STABLE
AS $$
    SELECT COUNT(*)::INT
    FROM dbo.UserReportingMapping_VisitReport m
    WHERE NULLIF(TRIM(p_EmpCode), '') IS NULL OR TRIM(m.EmpCode) = TRIM(p_EmpCode);
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_ReportingMapping_UpdateStatus(p_Id INT, p_IsActive BOOLEAN)
RETURNS TABLE (RowsAffected INT)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
DECLARE v_Count INT := 0;
BEGIN
    UPDATE dbo.UserReportingMapping_VisitReport SET IsActive = p_IsActive WHERE Id = p_Id;
    GET DIAGNOSTICS v_Count = ROW_COUNT;
    RETURN QUERY SELECT v_Count;
END;
$$;
