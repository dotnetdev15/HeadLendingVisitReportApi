USE [SBIPDLCOL]
GO

/* Ensure observation image columns + FormRole exist for print/download */
IF COL_LENGTH('dbo.LendingVisitReports', 'BusinessTypeImg') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD BusinessTypeImg NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.LendingVisitReports', 'CashTypeImg') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD CashTypeImg NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.LendingVisitReports', 'OdTypeImg') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD OdTypeImg NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.LendingVisitReports', 'ODTypeImg') IS NULL
    AND COL_LENGTH('dbo.LendingVisitReports', 'OdTypeImg') IS NOT NULL
    PRINT 'OdTypeImg already present';
IF COL_LENGTH('dbo.LendingVisitReports', 'ODTypeImg') IS NULL
    AND COL_LENGTH('dbo.LendingVisitReports', 'OdTypeImg') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD ODTypeImg NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.LendingVisitReports', 'NpaTypeImg') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD NpaTypeImg NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.LendingVisitReports', 'CollectionTypeImg') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD CollectionTypeImg NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.LendingVisitReports', 'AuditTypeImg') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD AuditTypeImg NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.LendingVisitReports', 'OthersTypeImg') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD OthersTypeImg NVARCHAR(MAX) NULL;
IF COL_LENGTH('dbo.LendingVisitReports', 'FormRole') IS NULL
    ALTER TABLE dbo.LendingVisitReports ADD FormRole NVARCHAR(50) NULL;
GO

IF COL_LENGTH('dbo.LendingVisitAttachments', 'DocumentCategory') IS NULL
    ALTER TABLE dbo.LendingVisitAttachments ADD DocumentCategory NVARCHAR(50) NULL;
GO
