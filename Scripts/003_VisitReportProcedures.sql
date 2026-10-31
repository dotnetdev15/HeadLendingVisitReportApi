/* =====================================================================
   Stored Procedures — Visit Report CRUD
   ===================================================================== */
USE HeadLendingVisitReportDb;
GO

/* ---------------------------------------------------------------------
   usp_VisitReport_Create
   Inserts the header row plus every dynamic row-section in one
   transaction, using Table-Valued Parameters for the row sets.
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_VisitReport_Create', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_VisitReport_Create;
GO
CREATE PROCEDURE dbo.usp_VisitReport_Create
(
    @BranchName         NVARCHAR(200),
    @HeadName           NVARCHAR(200),
    @EmployeeCode       NVARCHAR(50),
    @VisitDateTime      DATETIME2,

    @DemandForMonth     DECIMAL(18,2),
    @CollectionAmount   DECIMAL(18,2),
    @Shortfall          DECIMAL(18,2),
    @CollectionPercent  DECIMAL(5,2),
    @CollectionRemarks  NVARCHAR(500),

    @ObsBusiness        NVARCHAR(MAX),
    @ObsCash            NVARCHAR(MAX),
    @ObsOd              NVARCHAR(MAX),
    @ObsNpa             NVARCHAR(MAX),
    @ObsCollection      NVARCHAR(MAX),
    @ObsAudit           NVARCHAR(MAX),
    @ObsOthers          NVARCHAR(MAX),

    @SignatureName      NVARCHAR(200),
    @SignatureDate      DATE,

    @BusinessDevelopments   dbo.BusinessDevelopmentRowType READONLY,
    @CashManagements        dbo.CashManagementRowType READONLY,
    @NpaOdManagements       dbo.NpaOdManagementRowType READONLY,
    @NpaBorrowers           dbo.NpaBorrowerRowType READONLY,
    @ConcurrentAudits       dbo.ConcurrentAuditRowType READONLY
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    BEGIN TRANSACTION;

    INSERT INTO dbo.LendingVisitReports
        (BranchName, HeadName, EmployeeCode, VisitDateTime,
         DemandForMonth, CollectionAmount, Shortfall, CollectionPercent, CollectionRemarks,
         ObsBusiness, ObsCash, ObsOd, ObsNpa, ObsCollection, ObsAudit, ObsOthers,
         SignatureName, SignatureDate)
    VALUES
        (@BranchName, @HeadName, @EmployeeCode, @VisitDateTime,
         @DemandForMonth, @CollectionAmount, @Shortfall, @CollectionPercent, @CollectionRemarks,
         @ObsBusiness, @ObsCash, @ObsOd, @ObsNpa, @ObsCollection, @ObsAudit, @ObsOthers,
         @SignatureName, @SignatureDate);

    DECLARE @NewId INT = SCOPE_IDENTITY();

    INSERT INTO dbo.LendingVisitBusinessDevelopments (VisitReportId, RowOrder, TypeOfLoan, Budget, Sanctioned, Disbursed)
    SELECT @NewId, RowOrder, TypeOfLoan, Budget, Sanctioned, Disbursed FROM @BusinessDevelopments;

    INSERT INTO dbo.LendingVisitCashManagements (VisitReportId, RowOrder, Particulars, Observation, Remarks)
    SELECT @NewId, RowOrder, Particulars, Observation, Remarks FROM @CashManagements;

    INSERT INTO dbo.LendingVisitNpaOdManagements (VisitReportId, RowOrder, Particulars, Amount, RecoveryMade, Remarks)
    SELECT @NewId, RowOrder, Particulars, Amount, RecoveryMade, Remarks FROM @NpaOdManagements;

    INSERT INTO dbo.LendingVisitNpaBorrowers (VisitReportId, RowOrder, BorrowerName, AccountNo, AmountRecovered, Remarks)
    SELECT @NewId, RowOrder, BorrowerName, AccountNo, AmountRecovered, Remarks FROM @NpaBorrowers;

    INSERT INTO dbo.LendingVisitConcurrentAudits (VisitReportId, RowOrder, AuditorObservation, BranchCompliance, Remarks)
    SELECT @NewId, RowOrder, AuditorObservation, BranchCompliance, Remarks FROM @ConcurrentAudits;

    COMMIT TRANSACTION;

    SELECT @NewId AS Id;
END
GO

/* ---------------------------------------------------------------------
   usp_VisitReport_Update
   Updates the header row; replaces each child row-set wholesale
   (delete-then-insert) so it always matches exactly what the caller
   currently has on the form, however many rows were added/removed.
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_VisitReport_Update', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_VisitReport_Update;
GO
CREATE PROCEDURE dbo.usp_VisitReport_Update
(
    @Id                 INT,
    @BranchName         NVARCHAR(200),
    @HeadName           NVARCHAR(200),
    @EmployeeCode       NVARCHAR(50),
    @VisitDateTime      DATETIME2,

    @DemandForMonth     DECIMAL(18,2),
    @CollectionAmount   DECIMAL(18,2),
    @Shortfall          DECIMAL(18,2),
    @CollectionPercent  DECIMAL(5,2),
    @CollectionRemarks  NVARCHAR(500),

    @ObsBusiness        NVARCHAR(MAX),
    @ObsCash            NVARCHAR(MAX),
    @ObsOd              NVARCHAR(MAX),
    @ObsNpa             NVARCHAR(MAX),
    @ObsCollection      NVARCHAR(MAX),
    @ObsAudit           NVARCHAR(MAX),
    @ObsOthers          NVARCHAR(MAX),

    @SignatureName      NVARCHAR(200),
    @SignatureDate      DATE,

    @BusinessDevelopments   dbo.BusinessDevelopmentRowType READONLY,
    @CashManagements        dbo.CashManagementRowType READONLY,
    @NpaOdManagements       dbo.NpaOdManagementRowType READONLY,
    @NpaBorrowers           dbo.NpaBorrowerRowType READONLY,
    @ConcurrentAudits       dbo.ConcurrentAuditRowType READONLY
)
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    -- No RAISERROR here: a not-found update stays a clean "no rows" result
    -- (ExecuteScalar returns NULL) rather than throwing a SqlException,
    -- so the API layer can turn it into an ordinary 404 response.
    DECLARE @RowsAffected INT = 0;

    BEGIN TRANSACTION;

    UPDATE dbo.LendingVisitReports
    SET BranchName        = @BranchName,
        HeadName          = @HeadName,
        EmployeeCode      = @EmployeeCode,
        VisitDateTime     = @VisitDateTime,
        DemandForMonth    = @DemandForMonth,
        CollectionAmount  = @CollectionAmount,
        Shortfall         = @Shortfall,
        CollectionPercent = @CollectionPercent,
        CollectionRemarks = @CollectionRemarks,
        ObsBusiness       = @ObsBusiness,
        ObsCash           = @ObsCash,
        ObsOd             = @ObsOd,
        ObsNpa            = @ObsNpa,
        ObsCollection     = @ObsCollection,
        ObsAudit          = @ObsAudit,
        ObsOthers         = @ObsOthers,
        SignatureName     = @SignatureName,
        SignatureDate     = @SignatureDate,
        UpdatedAtUtc      = SYSUTCDATETIME()
    WHERE Id = @Id;

    SET @RowsAffected = @@ROWCOUNT;

    IF @RowsAffected > 0
    BEGIN
        DELETE FROM dbo.LendingVisitBusinessDevelopments WHERE VisitReportId = @Id;
        INSERT INTO dbo.LendingVisitBusinessDevelopments (VisitReportId, RowOrder, TypeOfLoan, Budget, Sanctioned, Disbursed)
        SELECT @Id, RowOrder, TypeOfLoan, Budget, Sanctioned, Disbursed FROM @BusinessDevelopments;

        DELETE FROM dbo.LendingVisitCashManagements WHERE VisitReportId = @Id;
        INSERT INTO dbo.LendingVisitCashManagements (VisitReportId, RowOrder, Particulars, Observation, Remarks)
        SELECT @Id, RowOrder, Particulars, Observation, Remarks FROM @CashManagements;

        DELETE FROM dbo.LendingVisitNpaOdManagements WHERE VisitReportId = @Id;
        INSERT INTO dbo.LendingVisitNpaOdManagements (VisitReportId, RowOrder, Particulars, Amount, RecoveryMade, Remarks)
        SELECT @Id, RowOrder, Particulars, Amount, RecoveryMade, Remarks FROM @NpaOdManagements;

        DELETE FROM dbo.LendingVisitNpaBorrowers WHERE VisitReportId = @Id;
        INSERT INTO dbo.LendingVisitNpaBorrowers (VisitReportId, RowOrder, BorrowerName, AccountNo, AmountRecovered, Remarks)
        SELECT @Id, RowOrder, BorrowerName, AccountNo, AmountRecovered, Remarks FROM @NpaBorrowers;

        DELETE FROM dbo.LendingVisitConcurrentAudits WHERE VisitReportId = @Id;
        INSERT INTO dbo.LendingVisitConcurrentAudits (VisitReportId, RowOrder, AuditorObservation, BranchCompliance, Remarks)
        SELECT @Id, RowOrder, AuditorObservation, BranchCompliance, Remarks FROM @ConcurrentAudits;
    END

    COMMIT TRANSACTION;

    IF @RowsAffected > 0
        SELECT @Id AS Id;
    -- else: no result set is returned, so ExecuteScalar() on the caller
    -- side comes back NULL and the API can respond 404.
END
GO

/* ---------------------------------------------------------------------
   usp_VisitReport_GetById
   Returns 7 result sets, read in order by the API layer:
   1) header  2) business developments  3) cash managements
   4) npa/od managements  5) npa borrowers  6) concurrent audits
   7) attachments
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_VisitReport_GetById', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_VisitReport_GetById;
GO
CREATE PROCEDURE dbo.usp_VisitReport_GetById
(
    @Id INT
)
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM dbo.LendingVisitReports WHERE Id = @Id;

    SELECT * FROM dbo.LendingVisitBusinessDevelopments WHERE VisitReportId = @Id ORDER BY RowOrder;
    SELECT * FROM dbo.LendingVisitCashManagements WHERE VisitReportId = @Id ORDER BY RowOrder;
    SELECT * FROM dbo.LendingVisitNpaOdManagements WHERE VisitReportId = @Id ORDER BY RowOrder;
    SELECT * FROM dbo.LendingVisitNpaBorrowers WHERE VisitReportId = @Id ORDER BY RowOrder;
    SELECT * FROM dbo.LendingVisitConcurrentAudits WHERE VisitReportId = @Id ORDER BY RowOrder;
    SELECT * FROM dbo.LendingVisitAttachments WHERE VisitReportId = @Id ORDER BY UploadedAtUtc;
END
GO

/* ---------------------------------------------------------------------
   usp_VisitReport_GetAll
   Paged list of report headers (no child rows) with optional filters.
   Result set 1 = page of rows, result set 2 = total matching count.
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_VisitReport_GetAll', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_VisitReport_GetAll;
GO
CREATE PROCEDURE dbo.usp_VisitReport_GetAll
(
    @Page           INT = 1,
    @PageSize       INT = 20,
    @BranchName     NVARCHAR(200) = NULL,
    @EmployeeCode   NVARCHAR(50)  = NULL,
    @FromDate       DATETIME2     = NULL,
    @ToDate         DATETIME2     = NULL
)
AS
BEGIN
    SET NOCOUNT ON;

    IF @Page < 1 SET @Page = 1;
    IF @PageSize < 1 SET @PageSize = 20;

    ;WITH Filtered AS
    (
        SELECT *
        FROM dbo.LendingVisitReports
        WHERE (@BranchName   IS NULL OR BranchName   LIKE '%' + @BranchName + '%')
          AND (@EmployeeCode IS NULL OR EmployeeCode  = @EmployeeCode)
          AND (@FromDate     IS NULL OR VisitDateTime >= @FromDate)
          AND (@ToDate       IS NULL OR VisitDateTime <= @ToDate)
    )
    SELECT *
    FROM Filtered
    ORDER BY CreatedAtUtc DESC
    OFFSET (@Page - 1) * @PageSize ROWS FETCH NEXT @PageSize ROWS ONLY;

    ;WITH Filtered AS
    (
        SELECT Id
        FROM dbo.LendingVisitReports
        WHERE (@BranchName   IS NULL OR BranchName   LIKE '%' + @BranchName + '%')
          AND (@EmployeeCode IS NULL OR EmployeeCode  = @EmployeeCode)
          AND (@FromDate     IS NULL OR VisitDateTime >= @FromDate)
          AND (@ToDate       IS NULL OR VisitDateTime <= @ToDate)
    )
    SELECT COUNT(*) AS TotalCount FROM Filtered;
END
GO

/* ---------------------------------------------------------------------
   usp_VisitReport_Delete
   Child rows and attachment metadata cascade-delete via FK constraints.
   NOTE: the API must fetch attachment file paths (usp_Attachment_GetByVisitReportId)
   and remove the physical files BEFORE calling this procedure.
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.usp_VisitReport_Delete', 'P') IS NOT NULL DROP PROCEDURE dbo.usp_VisitReport_Delete;
GO
CREATE PROCEDURE dbo.usp_VisitReport_Delete
(
    @Id INT
)
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.LendingVisitReports WHERE Id = @Id;
    SELECT @@ROWCOUNT AS RowsDeleted;
END
GO
