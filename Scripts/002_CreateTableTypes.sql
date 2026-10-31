/* =====================================================================
   User-Defined Table Types
   Used as Table-Valued Parameters so the API can pass a whole dynamic
   row set (however many rows the user added on the form) to a single
   stored procedure call, in one round trip and one transaction.
   ===================================================================== */
USE HeadLendingVisitReportDb;
GO

IF TYPE_ID('dbo.BusinessDevelopmentRowType') IS NOT NULL DROP TYPE dbo.BusinessDevelopmentRowType;
GO
CREATE TYPE dbo.BusinessDevelopmentRowType AS TABLE
(
    RowOrder    INT             NOT NULL,
    TypeOfLoan  NVARCHAR(200)   NULL,
    Budget      DECIMAL(18,2)   NULL,
    Sanctioned  DECIMAL(18,2)   NULL,
    Disbursed   DECIMAL(18,2)   NULL
);
GO

IF TYPE_ID('dbo.CashManagementRowType') IS NOT NULL DROP TYPE dbo.CashManagementRowType;
GO
CREATE TYPE dbo.CashManagementRowType AS TABLE
(
    RowOrder     INT             NOT NULL,
    Particulars  NVARCHAR(300)   NULL,
    Observation  NVARCHAR(10)    NULL,
    Remarks      NVARCHAR(500)   NULL
);
GO

IF TYPE_ID('dbo.NpaOdManagementRowType') IS NOT NULL DROP TYPE dbo.NpaOdManagementRowType;
GO
CREATE TYPE dbo.NpaOdManagementRowType AS TABLE
(
    RowOrder      INT             NOT NULL,
    Particulars   NVARCHAR(200)   NULL,
    Amount        DECIMAL(18,2)   NULL,
    RecoveryMade  DECIMAL(18,2)   NULL,
    Remarks       NVARCHAR(500)   NULL
);
GO

IF TYPE_ID('dbo.NpaBorrowerRowType') IS NOT NULL DROP TYPE dbo.NpaBorrowerRowType;
GO
CREATE TYPE dbo.NpaBorrowerRowType AS TABLE
(
    RowOrder        INT             NOT NULL,
    BorrowerName    NVARCHAR(200)   NULL,
    AccountNo       NVARCHAR(50)    NULL,
    AmountRecovered DECIMAL(18,2)   NULL,
    Remarks         NVARCHAR(500)   NULL
);
GO

IF TYPE_ID('dbo.ConcurrentAuditRowType') IS NOT NULL DROP TYPE dbo.ConcurrentAuditRowType;
GO
CREATE TYPE dbo.ConcurrentAuditRowType AS TABLE
(
    RowOrder            INT             NOT NULL,
    AuditorObservation  NVARCHAR(1000)  NULL,
    BranchCompliance    NVARCHAR(10)    NULL,
    Remarks             NVARCHAR(500)   NULL
);
GO
