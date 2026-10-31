/* =====================================================================
   Head Lending Visit Report — Table Creation Script
   Run against the target SQL Server database (e.g. HeadLendingVisitReportDb)
   ===================================================================== */
IF DB_ID('HeadLendingVisitReportDb') IS NULL
BEGIN
    PRINT 'Creating database HeadLendingVisitReportDb...';
    CREATE DATABASE HeadLendingVisitReportDb;
END
GO

USE HeadLendingVisitReportDb;
GO

/* ---------------------------------------------------------------------
   Parent table — one row per submitted visit report
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.LendingVisitReports', 'U') IS NOT NULL DROP TABLE dbo.LendingVisitReports;
GO
CREATE TABLE dbo.LendingVisitReports
(
    Id                  INT IDENTITY(1,1)   NOT NULL CONSTRAINT PK_LendingVisitReports PRIMARY KEY,
    BranchName          NVARCHAR(200)       NULL,
    HeadName            NVARCHAR(200)       NULL,
    EmployeeCode        NVARCHAR(50)        NULL,
    VisitDateTime       DATETIME2           NULL,

    DemandForMonth      DECIMAL(18,2)       NULL,
    CollectionAmount    DECIMAL(18,2)       NULL,
    Shortfall           DECIMAL(18,2)       NULL,
    CollectionPercent   DECIMAL(5,2)        NULL,
    CollectionRemarks   NVARCHAR(500)       NULL,

    ObsBusiness         NVARCHAR(MAX)       NULL,
    ObsCash             NVARCHAR(MAX)       NULL,
    ObsOd               NVARCHAR(MAX)       NULL,
    ObsNpa              NVARCHAR(MAX)       NULL,
    ObsCollection       NVARCHAR(MAX)       NULL,
    ObsAudit            NVARCHAR(MAX)       NULL,
    ObsOthers           NVARCHAR(MAX)       NULL,

    SignatureName       NVARCHAR(200)       NULL,
    SignatureDate        DATE                NULL,

    CreatedAtUtc        DATETIME2           NOT NULL CONSTRAINT DF_LendingVisitReports_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
    UpdatedAtUtc         DATETIME2           NULL
);
GO

/* ---------------------------------------------------------------------
   Child tables — dynamic row sections (variable number of rows per report)
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.LendingVisitBusinessDevelopments', 'U') IS NOT NULL DROP TABLE dbo.LendingVisitBusinessDevelopments;
GO
CREATE TABLE dbo.LendingVisitBusinessDevelopments
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LendingVisitBusinessDevelopments PRIMARY KEY,
    VisitReportId   INT               NOT NULL,
    RowOrder        INT               NOT NULL DEFAULT (0),
    TypeOfLoan      NVARCHAR(200)     NULL,
    Budget          DECIMAL(18,2)     NULL,
    Sanctioned      DECIMAL(18,2)     NULL,
    Disbursed       DECIMAL(18,2)     NULL,
    CONSTRAINT FK_BusinessDevelopments_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_BusinessDevelopments_VisitReportId ON dbo.LendingVisitBusinessDevelopments (VisitReportId);
GO

IF OBJECT_ID('dbo.LendingVisitCashManagements', 'U') IS NOT NULL DROP TABLE dbo.LendingVisitCashManagements;
GO
CREATE TABLE dbo.LendingVisitCashManagements
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LendingVisitCashManagements PRIMARY KEY,
    VisitReportId   INT               NOT NULL,
    RowOrder        INT               NOT NULL DEFAULT (0),
    Particulars     NVARCHAR(300)     NULL,
    Observation     NVARCHAR(10)      NULL, -- 'Yes' / 'No'
    Remarks         NVARCHAR(500)     NULL,
    CONSTRAINT FK_CashManagements_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_CashManagements_VisitReportId ON dbo.LendingVisitCashManagements (VisitReportId);
GO

IF OBJECT_ID('dbo.LendingVisitNpaOdManagements', 'U') IS NOT NULL DROP TABLE dbo.LendingVisitNpaOdManagements;
GO
CREATE TABLE dbo.LendingVisitNpaOdManagements
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LendingVisitNpaOdManagements PRIMARY KEY,
    VisitReportId   INT               NOT NULL,
    RowOrder        INT               NOT NULL DEFAULT (0),
    Particulars     NVARCHAR(200)     NULL, -- e.g. '1 EMI Due', '2 EMI Due', '3 EMI Due', 'NPA'
    Amount          DECIMAL(18,2)     NULL,
    RecoveryMade    DECIMAL(18,2)     NULL,
    Remarks         NVARCHAR(500)     NULL,
    CONSTRAINT FK_NpaOdManagements_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_NpaOdManagements_VisitReportId ON dbo.LendingVisitNpaOdManagements (VisitReportId);
GO

IF OBJECT_ID('dbo.LendingVisitNpaBorrowers', 'U') IS NOT NULL DROP TABLE dbo.LendingVisitNpaBorrowers;
GO
CREATE TABLE dbo.LendingVisitNpaBorrowers
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LendingVisitNpaBorrowers PRIMARY KEY,
    VisitReportId   INT               NOT NULL,
    RowOrder        INT               NOT NULL DEFAULT (0),
    BorrowerName    NVARCHAR(200)     NULL,
    AccountNo       NVARCHAR(50)      NULL,
    AmountRecovered DECIMAL(18,2)     NULL,
    Remarks         NVARCHAR(500)     NULL,
    CONSTRAINT FK_NpaBorrowers_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_NpaBorrowers_VisitReportId ON dbo.LendingVisitNpaBorrowers (VisitReportId);
GO

IF OBJECT_ID('dbo.LendingVisitConcurrentAudits', 'U') IS NOT NULL DROP TABLE dbo.LendingVisitConcurrentAudits;
GO
CREATE TABLE dbo.LendingVisitConcurrentAudits
(
    Id                  INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LendingVisitConcurrentAudits PRIMARY KEY,
    VisitReportId       INT               NOT NULL,
    RowOrder            INT               NOT NULL DEFAULT (0),
    AuditorObservation  NVARCHAR(1000)    NULL,
    BranchCompliance    NVARCHAR(10)      NULL, -- 'Correct' / 'False'
    Remarks             NVARCHAR(500)     NULL,
    CONSTRAINT FK_ConcurrentAudits_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_ConcurrentAudits_VisitReportId ON dbo.LendingVisitConcurrentAudits (VisitReportId);
GO

/* ---------------------------------------------------------------------
   Attachments — files stored on the file system, metadata in SQL Server
   --------------------------------------------------------------------- */
IF OBJECT_ID('dbo.LendingVisitAttachments', 'U') IS NOT NULL DROP TABLE dbo.LendingVisitAttachments;
GO
CREATE TABLE dbo.LendingVisitAttachments
(
    Id              INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_LendingVisitAttachments PRIMARY KEY,
    VisitReportId   INT               NOT NULL,
    FileName        NVARCHAR(260)     NOT NULL, -- original uploaded file name
    StoredFileName  NVARCHAR(260)     NOT NULL, -- unique name used on disk
    FilePath        NVARCHAR(500)     NOT NULL, -- relative path under FileStorage:RootPath
    ContentType     NVARCHAR(100)     NULL,
    FileSizeBytes   BIGINT            NOT NULL DEFAULT (0),
    UploadedAtUtc   DATETIME2         NOT NULL CONSTRAINT DF_LendingVisitAttachments_UploadedAtUtc DEFAULT (SYSUTCDATETIME()),
    CONSTRAINT FK_Attachments_VisitReports FOREIGN KEY (VisitReportId)
        REFERENCES dbo.LendingVisitReports (Id) ON DELETE CASCADE
);
GO
CREATE INDEX IX_Attachments_VisitReportId ON dbo.LendingVisitAttachments (VisitReportId);
GO
