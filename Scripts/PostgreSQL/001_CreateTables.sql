/* =====================================================================
   Head Lending Visit Report — PostgreSQL 10.3 table creation
   (port of Scripts/001_CreateTables.sql + 015_ObservationImages_FormRole.sql)

   Run with psql against the target database, e.g.
     psql -h <host> -U <user> -d <database> -f 001_CreateTables.sql

   Identifiers are unquoted (stored lower-case). Npgsql's reader matches
   column names case-insensitively, so the C# code keeps reading
   "VisitReportId", "CreatedAtUtc", etc. unchanged.
   ===================================================================== */

CREATE SCHEMA IF NOT EXISTS dbo;

/* ---------------------------------------------------------------------
   Parent table — one row per submitted visit report
   --------------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS dbo.LendingVisitReports
(
    Id                  SERIAL          NOT NULL CONSTRAINT PK_LendingVisitReports PRIMARY KEY,
    BranchName          VARCHAR(200)    NULL,
    HeadName            VARCHAR(200)    NULL,
    EmployeeCode        VARCHAR(50)     NULL,
    VisitDateTime       TIMESTAMP       NULL,

    DemandForMonth      NUMERIC(18,2)   NULL,
    CollectionAmount    NUMERIC(18,2)   NULL,
    Shortfall           NUMERIC(18,2)   NULL,
    CollectionPercent   NUMERIC(5,2)    NULL,
    CollectionRemarks   VARCHAR(500)    NULL,

    ObsBusiness         TEXT            NULL,
    ObsCash             TEXT            NULL,
    ObsOd               TEXT            NULL,
    ObsNpa              TEXT            NULL,
    ObsCollection       TEXT            NULL,
    ObsAudit            TEXT            NULL,
    ObsOthers           TEXT            NULL,

    SignatureName       VARCHAR(200)    NULL,
    SignatureDate       DATE            NULL,

    CreatedAtUtc        TIMESTAMP       NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    UpdatedAtUtc        TIMESTAMP       NULL,

    -- from 015_ObservationImages_FormRole.sql
    BusinessTypeImg     TEXT            NULL,
    CashTypeImg         TEXT            NULL,
    OdTypeImg           TEXT            NULL,
    NpaTypeImg          TEXT            NULL,
    CollectionTypeImg   TEXT            NULL,
    AuditTypeImg        TEXT            NULL,
    OthersTypeImg       TEXT            NULL,
    FormRole            VARCHAR(50)     NULL
);

/* ---------------------------------------------------------------------
   Child tables — dynamic row sections
   --------------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS dbo.LendingVisitBusinessDevelopments
(
    Id              SERIAL          NOT NULL CONSTRAINT PK_LendingVisitBusinessDevelopments PRIMARY KEY,
    VisitReportId   INT             NOT NULL,
    RowOrder        INT             NOT NULL DEFAULT (0),
    TypeOfLoan      VARCHAR(200)    NULL,
    Budget          NUMERIC(18,2)   NULL,
    Sanctioned      NUMERIC(18,2)   NULL,
    Disbursed       NUMERIC(18,2)   NULL,
    CONSTRAINT FK_BusinessDevelopments_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_BusinessDevelopments_VisitReportId ON dbo.LendingVisitBusinessDevelopments (VisitReportId);

CREATE TABLE IF NOT EXISTS dbo.LendingVisitCashManagements
(
    Id              SERIAL          NOT NULL CONSTRAINT PK_LendingVisitCashManagements PRIMARY KEY,
    VisitReportId   INT             NOT NULL,
    RowOrder        INT             NOT NULL DEFAULT (0),
    Particulars     VARCHAR(300)    NULL,
    Observation     VARCHAR(10)     NULL, -- 'Yes' / 'No'
    Remarks         VARCHAR(500)    NULL,
    CONSTRAINT FK_CashManagements_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_CashManagements_VisitReportId ON dbo.LendingVisitCashManagements (VisitReportId);

CREATE TABLE IF NOT EXISTS dbo.LendingVisitNpaOdManagements
(
    Id              SERIAL          NOT NULL CONSTRAINT PK_LendingVisitNpaOdManagements PRIMARY KEY,
    VisitReportId   INT             NOT NULL,
    RowOrder        INT             NOT NULL DEFAULT (0),
    Particulars     VARCHAR(200)    NULL,
    Amount          NUMERIC(18,2)   NULL,
    RecoveryMade    NUMERIC(18,2)   NULL,
    Remarks         VARCHAR(500)    NULL,
    CONSTRAINT FK_NpaOdManagements_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_NpaOdManagements_VisitReportId ON dbo.LendingVisitNpaOdManagements (VisitReportId);

CREATE TABLE IF NOT EXISTS dbo.LendingVisitNpaBorrowers
(
    Id              SERIAL          NOT NULL CONSTRAINT PK_LendingVisitNpaBorrowers PRIMARY KEY,
    VisitReportId   INT             NOT NULL,
    RowOrder        INT             NOT NULL DEFAULT (0),
    BorrowerName    VARCHAR(200)    NULL,
    AccountNo       VARCHAR(50)     NULL,
    AmountRecovered NUMERIC(18,2)   NULL,
    Remarks         VARCHAR(500)    NULL,
    CONSTRAINT FK_NpaBorrowers_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_NpaBorrowers_VisitReportId ON dbo.LendingVisitNpaBorrowers (VisitReportId);

CREATE TABLE IF NOT EXISTS dbo.LendingVisitConcurrentAudits
(
    Id                  SERIAL          NOT NULL CONSTRAINT PK_LendingVisitConcurrentAudits PRIMARY KEY,
    VisitReportId       INT             NOT NULL,
    RowOrder            INT             NOT NULL DEFAULT (0),
    AuditorObservation  VARCHAR(1000)   NULL,
    BranchCompliance    VARCHAR(10)     NULL, -- 'Correct' / 'False'
    Remarks             VARCHAR(500)    NULL,
    CONSTRAINT FK_ConcurrentAudits_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_ConcurrentAudits_VisitReportId ON dbo.LendingVisitConcurrentAudits (VisitReportId);

/* ---------------------------------------------------------------------
   Attachments — files on disk, metadata in PostgreSQL
   --------------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS dbo.LendingVisitAttachments
(
    Id              SERIAL          NOT NULL CONSTRAINT PK_LendingVisitAttachments PRIMARY KEY,
    VisitReportId   INT             NOT NULL,
    FileName        VARCHAR(260)    NOT NULL,
    StoredFileName  VARCHAR(260)    NOT NULL,
    FilePath        VARCHAR(500)    NOT NULL,
    ContentType     VARCHAR(100)    NULL,
    FileSizeBytes   BIGINT          NOT NULL DEFAULT (0),
    UploadedAtUtc   TIMESTAMP       NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    DocumentCategory VARCHAR(50)    NULL,   -- from 015
    CONSTRAINT FK_Attachments_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Attachments_VisitReportId ON dbo.LendingVisitAttachments (VisitReportId);

/* ---------------------------------------------------------------------
   Comments (used by usp_VisitReport_AddComment / GetComments)
   --------------------------------------------------------------------- */
CREATE TABLE IF NOT EXISTS dbo.LendingVisitReportComments
(
    Id              SERIAL          NOT NULL CONSTRAINT PK_LendingVisitReportComments PRIMARY KEY,
    VisitReportId   INT             NOT NULL,
    EmployeeCode    VARCHAR(50)     NULL,
    CommentText     TEXT            NOT NULL DEFAULT '',
    CreatedByEmail  VARCHAR(200)    NULL,
    CreatedByName   VARCHAR(200)    NULL,
    CreatedAtUtc    TIMESTAMP       NOT NULL DEFAULT (now() AT TIME ZONE 'utc'),
    CONSTRAINT FK_Comments_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
CREATE INDEX IF NOT EXISTS IX_Comments_VisitReportId ON dbo.LendingVisitReportComments (VisitReportId);
