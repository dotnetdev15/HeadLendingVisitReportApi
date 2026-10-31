/* =====================================================================
   Attachments — PostgreSQL 10.3 port of 004_AttachmentProcedures.sql
   ===================================================================== */

CREATE OR REPLACE FUNCTION dbo.usp_Attachment_Insert
(
    p_VisitReportId  INT,
    p_FileName       VARCHAR,
    p_StoredFileName VARCHAR,
    p_FilePath       VARCHAR,
    p_ContentType    VARCHAR,
    p_FileSizeBytes  BIGINT
)
RETURNS TABLE (Id INT)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
BEGIN
    -- Parent missing -> return no row (ExecuteScalar null) -> API 404.
    IF NOT EXISTS (SELECT 1 FROM dbo.LendingVisitReports r WHERE r.Id = p_VisitReportId) THEN
        RETURN;
    END IF;

    RETURN QUERY
    INSERT INTO dbo.LendingVisitAttachments
        (VisitReportId, FileName, StoredFileName, FilePath, ContentType, FileSizeBytes)
    VALUES
        (p_VisitReportId, p_FileName, p_StoredFileName, p_FilePath, p_ContentType, p_FileSizeBytes)
    RETURNING dbo.LendingVisitAttachments.Id;
END;
$$;

CREATE OR REPLACE FUNCTION dbo.usp_Attachment_GetByVisitReportId(p_VisitReportId INT)
RETURNS SETOF dbo.LendingVisitAttachments
LANGUAGE sql STABLE
AS $$ SELECT * FROM dbo.LendingVisitAttachments WHERE VisitReportId = p_VisitReportId ORDER BY UploadedAtUtc; $$;

CREATE OR REPLACE FUNCTION dbo.usp_Attachment_GetById(p_Id INT)
RETURNS SETOF dbo.LendingVisitAttachments
LANGUAGE sql STABLE
AS $$ SELECT * FROM dbo.LendingVisitAttachments WHERE Id = p_Id; $$;

CREATE OR REPLACE FUNCTION dbo.usp_Attachment_Delete(p_Id INT)
RETURNS TABLE (RowsDeleted INT)
LANGUAGE plpgsql
AS $$
#variable_conflict use_column
DECLARE v_Count INT := 0;
BEGIN
    DELETE FROM dbo.LendingVisitAttachments WHERE Id = p_Id;
    GET DIAGNOSTICS v_Count = ROW_COUNT;
    RETURN QUERY SELECT v_Count;
END;
$$;
