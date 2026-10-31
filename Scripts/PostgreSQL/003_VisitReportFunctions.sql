/* =====================================================================
   Visit Report CRUD — PostgreSQL 10.3 port of 003_VisitReportProcedures.sql,
   008_usp_VisitReport_UploadDoc.sql and the comment procedures.

   PostgreSQL 10 has no CREATE PROCEDURE (added in v11), so every
   stored procedure is a FUNCTION. Table-Valued Parameters are replaced
   by JSONB arrays (one object per row, PascalCase keys) that are
   expanded with jsonb_to_recordset. A plpgsql function body runs in a
   single transaction, so Create/Update stay all-or-nothing.
   ===================================================================== */

/* ---------------------------------------------------------------------
   usp_VisitReport_Create — returns the new Id
   --------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_Create
(
    p_BranchName         VARCHAR,
    p_HeadName           VARCHAR,
    p_EmployeeCode       VARCHAR,
    p_VisitDateTime      TIMESTAMP,
    p_DemandForMonth     NUMERIC,
    p_CollectionAmount   NUMERIC,
    p_Shortfall          NUMERIC,
    p_CollectionPercent  NUMERIC,
    p_CollectionRemarks  VARCHAR,
    p_ObsBusiness        TEXT,
    p_ObsCash            TEXT,
    p_ObsOd              TEXT,
    p_ObsNpa             TEXT,
    p_ObsCollection      TEXT,
    p_ObsAudit           TEXT,
    p_ObsOthers          TEXT,
    p_SignatureName      VARCHAR,
    p_SignatureDate      DATE,
    p_BusinessDevelopments JSONB,
    p_CashManagements      JSONB,
    p_NpaOdManagements     JSONB,
    p_NpaBorrowers         JSONB,
    p_ConcurrentAudits     JSONB
)
RETURNS TABLE (Id INT)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
DECLARE
    v_NewId INT;
BEGIN
    INSERT INTO dbo.LendingVisitReports
        (BranchName, HeadName, EmployeeCode, VisitDateTime,
         DemandForMonth, CollectionAmount, Shortfall, CollectionPercent, CollectionRemarks,
         ObsBusiness, ObsCash, ObsOd, ObsNpa, ObsCollection, ObsAudit, ObsOthers,
         SignatureName, SignatureDate)
    VALUES
        (p_BranchName, p_HeadName, p_EmployeeCode, p_VisitDateTime,
         p_DemandForMonth, p_CollectionAmount, p_Shortfall, p_CollectionPercent, p_CollectionRemarks,
         p_ObsBusiness, p_ObsCash, p_ObsOd, p_ObsNpa, p_ObsCollection, p_ObsAudit, p_ObsOthers,
         p_SignatureName, p_SignatureDate)
    RETURNING dbo.LendingVisitReports.Id INTO v_NewId;

    PERFORM dbo.fn_VisitReport_InsertChildRows(v_NewId,
        p_BusinessDevelopments, p_CashManagements, p_NpaOdManagements, p_NpaBorrowers, p_ConcurrentAudits);

    RETURN QUERY SELECT v_NewId;
END;
$$;

/* ---------------------------------------------------------------------
   Helper: inserts every dynamic row-section for one report
   (shared by Create and Update — replaces the five TVP INSERT..SELECTs)
   --------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION dbo.fn_VisitReport_InsertChildRows
(
    p_Id                   INT,
    p_BusinessDevelopments JSONB,
    p_CashManagements      JSONB,
    p_NpaOdManagements     JSONB,
    p_NpaBorrowers         JSONB,
    p_ConcurrentAudits     JSONB
)
RETURNS VOID
LANGUAGE plpgsql
AS $$
BEGIN
    INSERT INTO dbo.LendingVisitBusinessDevelopments (VisitReportId, RowOrder, TypeOfLoan, Budget, Sanctioned, Disbursed)
    SELECT p_Id, x."RowOrder", x."TypeOfLoan", x."Budget", x."Sanctioned", x."Disbursed"
    FROM jsonb_to_recordset(COALESCE(p_BusinessDevelopments, '[]'::jsonb))
         AS x("RowOrder" INT, "TypeOfLoan" VARCHAR(200), "Budget" NUMERIC(18,2), "Sanctioned" NUMERIC(18,2), "Disbursed" NUMERIC(18,2));

    INSERT INTO dbo.LendingVisitCashManagements (VisitReportId, RowOrder, Particulars, Observation, Remarks)
    SELECT p_Id, x."RowOrder", x."Particulars", x."Observation", x."Remarks"
    FROM jsonb_to_recordset(COALESCE(p_CashManagements, '[]'::jsonb))
         AS x("RowOrder" INT, "Particulars" VARCHAR(300), "Observation" VARCHAR(10), "Remarks" VARCHAR(500));

    INSERT INTO dbo.LendingVisitNpaOdManagements (VisitReportId, RowOrder, Particulars, Amount, RecoveryMade, Remarks)
    SELECT p_Id, x."RowOrder", x."Particulars", x."Amount", x."RecoveryMade", x."Remarks"
    FROM jsonb_to_recordset(COALESCE(p_NpaOdManagements, '[]'::jsonb))
         AS x("RowOrder" INT, "Particulars" VARCHAR(200), "Amount" NUMERIC(18,2), "RecoveryMade" NUMERIC(18,2), "Remarks" VARCHAR(500));

    INSERT INTO dbo.LendingVisitNpaBorrowers (VisitReportId, RowOrder, BorrowerName, AccountNo, AmountRecovered, Remarks)
    SELECT p_Id, x."RowOrder", x."BorrowerName", x."AccountNo", x."AmountRecovered", x."Remarks"
    FROM jsonb_to_recordset(COALESCE(p_NpaBorrowers, '[]'::jsonb))
         AS x("RowOrder" INT, "BorrowerName" VARCHAR(200), "AccountNo" VARCHAR(50), "AmountRecovered" NUMERIC(18,2), "Remarks" VARCHAR(500));

    INSERT INTO dbo.LendingVisitConcurrentAudits (VisitReportId, RowOrder, AuditorObservation, BranchCompliance, Remarks)
    SELECT p_Id, x."RowOrder", x."AuditorObservation", x."BranchCompliance", x."Remarks"
    FROM jsonb_to_recordset(COALESCE(p_ConcurrentAudits, '[]'::jsonb))
         AS x("RowOrder" INT, "AuditorObservation" VARCHAR(1000), "BranchCompliance" VARCHAR(10), "Remarks" VARCHAR(500));
END;
$$;

/* ---------------------------------------------------------------------
   usp_VisitReport_Update
   Returns the Id when the row exists; returns NO rows when it doesn't,
   so ExecuteScalar() is null and the API answers 404 (same as before).
   --------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_Update
(
    p_Id                 INT,
    p_BranchName         VARCHAR,
    p_HeadName           VARCHAR,
    p_EmployeeCode       VARCHAR,
    p_VisitDateTime      TIMESTAMP,
    p_DemandForMonth     NUMERIC,
    p_CollectionAmount   NUMERIC,
    p_Shortfall          NUMERIC,
    p_CollectionPercent  NUMERIC,
    p_CollectionRemarks  VARCHAR,
    p_ObsBusiness        TEXT,
    p_ObsCash            TEXT,
    p_ObsOd              TEXT,
    p_ObsNpa             TEXT,
    p_ObsCollection      TEXT,
    p_ObsAudit           TEXT,
    p_ObsOthers          TEXT,
    p_SignatureName      VARCHAR,
    p_SignatureDate      DATE,
    p_BusinessDevelopments JSONB,
    p_CashManagements      JSONB,
    p_NpaOdManagements     JSONB,
    p_NpaBorrowers         JSONB,
    p_ConcurrentAudits     JSONB
)
RETURNS TABLE (Id INT)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
DECLARE
    v_RowsAffected INT := 0;
BEGIN
    UPDATE dbo.LendingVisitReports r
    SET BranchName        = p_BranchName,
        HeadName          = p_HeadName,
        EmployeeCode      = p_EmployeeCode,
        VisitDateTime     = p_VisitDateTime,
        DemandForMonth    = p_DemandForMonth,
        CollectionAmount  = p_CollectionAmount,
        Shortfall         = p_Shortfall,
        CollectionPercent = p_CollectionPercent,
        CollectionRemarks = p_CollectionRemarks,
        ObsBusiness       = p_ObsBusiness,
        ObsCash           = p_ObsCash,
        ObsOd             = p_ObsOd,
        ObsNpa            = p_ObsNpa,
        ObsCollection     = p_ObsCollection,
        ObsAudit          = p_ObsAudit,
        ObsOthers         = p_ObsOthers,
        SignatureName     = p_SignatureName,
        SignatureDate     = p_SignatureDate,
        UpdatedAtUtc      = (now() AT TIME ZONE 'utc')
    WHERE r.Id = p_Id;

    GET DIAGNOSTICS v_RowsAffected = ROW_COUNT;

    IF v_RowsAffected > 0 THEN
        DELETE FROM dbo.LendingVisitBusinessDevelopments WHERE VisitReportId = p_Id;
        DELETE FROM dbo.LendingVisitCashManagements      WHERE VisitReportId = p_Id;
        DELETE FROM dbo.LendingVisitNpaOdManagements     WHERE VisitReportId = p_Id;
        DELETE FROM dbo.LendingVisitNpaBorrowers         WHERE VisitReportId = p_Id;
        DELETE FROM dbo.LendingVisitConcurrentAudits     WHERE VisitReportId = p_Id;

        PERFORM dbo.fn_VisitReport_InsertChildRows(p_Id,
            p_BusinessDevelopments, p_CashManagements, p_NpaOdManagements, p_NpaBorrowers, p_ConcurrentAudits);

        RETURN QUERY SELECT p_Id;
    END IF;
    -- else: no row returned -> ExecuteScalar() is null -> 404
END;
$$;

/* ---------------------------------------------------------------------
   usp_VisitReport_GetById — was 7 result sets from one procedure.
   Now 7 functions; the API sends them as one batched command, so
   NextResultAsync() still walks them in the same order.
   --------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetById(p_Id INT)
RETURNS SETOF dbo.LendingVisitReports
LANGUAGE sql STABLE
AS $$ SELECT * FROM dbo.LendingVisitReports WHERE Id = p_Id; $$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetBusinessDevelopments(p_Id INT)
RETURNS SETOF dbo.LendingVisitBusinessDevelopments
LANGUAGE sql STABLE
AS $$ SELECT * FROM dbo.LendingVisitBusinessDevelopments WHERE VisitReportId = p_Id ORDER BY RowOrder; $$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetCashManagements(p_Id INT)
RETURNS SETOF dbo.LendingVisitCashManagements
LANGUAGE sql STABLE
AS $$ SELECT * FROM dbo.LendingVisitCashManagements WHERE VisitReportId = p_Id ORDER BY RowOrder; $$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetNpaOdManagements(p_Id INT)
RETURNS SETOF dbo.LendingVisitNpaOdManagements
LANGUAGE sql STABLE
AS $$ SELECT * FROM dbo.LendingVisitNpaOdManagements WHERE VisitReportId = p_Id ORDER BY RowOrder; $$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetNpaBorrowers(p_Id INT)
RETURNS SETOF dbo.LendingVisitNpaBorrowers
LANGUAGE sql STABLE
AS $$ SELECT * FROM dbo.LendingVisitNpaBorrowers WHERE VisitReportId = p_Id ORDER BY RowOrder; $$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetConcurrentAudits(p_Id INT)
RETURNS SETOF dbo.LendingVisitConcurrentAudits
LANGUAGE sql STABLE
AS $$ SELECT * FROM dbo.LendingVisitConcurrentAudits WHERE VisitReportId = p_Id ORDER BY RowOrder; $$;

/* ---------------------------------------------------------------------
   usp_VisitReport_Delete — children cascade via FK
   --------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_Delete(p_Id INT)
RETURNS TABLE (RowsDeleted INT)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
DECLARE v_Count INT := 0;
BEGIN
    DELETE FROM dbo.LendingVisitReports WHERE Id = p_Id;
    GET DIAGNOSTICS v_Count = ROW_COUNT;
    RETURN QUERY SELECT v_Count;
END;
$$;

/* ---------------------------------------------------------------------
   usp_VisitReport_UploadDoc (008) — Mode 'UPDATE' saves the observation
   image paths; Mode 'GET' returns the user/role row for an e-mail.
   --------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_UploadDoc
(
    p_VisitReportId      INT,
    p_BusinessTypeImg    TEXT,
    p_CashTypeImg        TEXT,
    p_OdTypeImg          TEXT,
    p_NpaTypeImg         TEXT,
    p_CollectionTypeImg  TEXT,
    p_AuditTypeImg       TEXT,
    p_OthersTypeImg      TEXT,
    p_Email              VARCHAR,
    p_Mode               VARCHAR
)
RETURNS TABLE (Name VARCHAR, RoleName VARCHAR, BranchCode VARCHAR, EmpCode VARCHAR, Email VARCHAR)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
BEGIN
    IF p_Mode = 'UPDATE' THEN
        UPDATE dbo.LendingVisitReports
        SET BusinessTypeImg   = p_BusinessTypeImg,
            CashTypeImg       = p_CashTypeImg,
            OdTypeImg         = p_OdTypeImg,
            NpaTypeImg        = p_NpaTypeImg,
            CollectionTypeImg = p_CollectionTypeImg,
            AuditTypeImg      = p_AuditTypeImg,
            OthersTypeImg     = p_OthersTypeImg
        WHERE Id = p_VisitReportId;
    END IF;

    IF p_Mode = 'GET' THEN
        RETURN QUERY
        SELECT
            um.Name::VARCHAR,
            m.EmpType::VARCHAR,
            um.BranchCode::VARCHAR,
            COALESCE(NULLIF(TRIM(um.EmpCode), ''), um.OldEmpCode)::VARCHAR(50),
            um.Email::VARCHAR
        FROM dbo.UsersMasters um
        LEFT JOIN dbo.UserReportingMappingMasters m
            ON COALESCE(m.IsActive, TRUE) = TRUE
           AND (
                TRIM(m.EmpCode)    = COALESCE(NULLIF(TRIM(um.EmpCode), ''), um.OldEmpCode)
                OR TRIM(m.OldEmpCode) = COALESCE(NULLIF(TRIM(um.EmpCode), ''), um.OldEmpCode)
               )
        WHERE um.IsActive = TRUE AND um.IsDeleted = FALSE AND LOWER(um.Email) = LOWER(p_Email);
    END IF;
END;
$$;

/* ---------------------------------------------------------------------
   Comments
   --------------------------------------------------------------------- */
CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_AddComment
(
    p_VisitReportId   INT,
    p_EmployeeCode    VARCHAR,
    p_CommentText     TEXT,
    p_CreatedByEmail  VARCHAR,
    p_CreatedByName   VARCHAR
)
RETURNS TABLE (Id INT)
LANGUAGE sql
AS $$
    INSERT INTO dbo.LendingVisitReportComments (VisitReportId, EmployeeCode, CommentText, CreatedByEmail, CreatedByName)
    VALUES (p_VisitReportId, p_EmployeeCode, COALESCE(p_CommentText, ''), p_CreatedByEmail, p_CreatedByName)
    RETURNING Id;
$$;

CREATE OR REPLACE FUNCTION dbo.usp_VisitReport_GetComments(p_VisitReportId INT)
RETURNS SETOF dbo.LendingVisitReportComments
LANGUAGE sql STABLE
AS $$
    SELECT * FROM dbo.LendingVisitReportComments
    WHERE VisitReportId = p_VisitReportId
    ORDER BY CreatedAtUtc DESC, Id DESC;
$$;
