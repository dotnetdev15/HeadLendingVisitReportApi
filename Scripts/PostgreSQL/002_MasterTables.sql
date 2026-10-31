/* =====================================================================
   ERP master / mapping tables used by the visit-report functions.

   In SQL Server these already existed in the ERP database (PDLERP /
   SBIPDLCOL) and were NOT part of this project's scripts. If you are
   migrating those tables' data into PostgreSQL yourself, SKIP this file
   (or make sure your migrated tables carry at least these columns).
   The definitions below are the minimum columns the functions read.
   ===================================================================== */

CREATE SCHEMA IF NOT EXISTS dbo;

CREATE TABLE IF NOT EXISTS dbo.UsersMasters
(
    Id          SERIAL        PRIMARY KEY,
    Name        VARCHAR(200)  NULL,
    Email       VARCHAR(256)  NULL,
    EmpCode     VARCHAR(50)   NULL,
    OldEmpCode  VARCHAR(50)   NULL,
    BranchCode  VARCHAR(50)   NULL,
    IsActive    BOOLEAN       NULL DEFAULT TRUE,
    IsDeleted   BOOLEAN       NULL DEFAULT FALSE
);

CREATE TABLE IF NOT EXISTS dbo.UserReportingMappingMasters
(
    Id             SERIAL       PRIMARY KEY,
    EmpCode        VARCHAR(50)  NULL,
    OldEmpCode     VARCHAR(50)  NULL,
    ParentEmpCode  VARCHAR(50)  NULL,
    EmpType        VARCHAR(50)  NULL,
    IsActive       BOOLEAN      NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS dbo.UserReportingMapping_VisitReport
(
    Id             SERIAL       PRIMARY KEY,
    EmpCode        VARCHAR(50)  NULL,
    ParentEmpCode  VARCHAR(50)  NULL,
    EmpType        VARCHAR(50)  NULL,
    IsActive       BOOLEAN      NULL DEFAULT TRUE
);

CREATE TABLE IF NOT EXISTS dbo.UserCreatorMapping_VisitReport
(
    Id          SERIAL        PRIMARY KEY,
    EmpCode     VARCHAR(50)   NULL,
    Creator     VARCHAR(100)  NULL,
    Branch      VARCHAR(200)  NULL,
    Code        VARCHAR(50)   NULL,
    IsActive    BOOLEAN       NULL DEFAULT TRUE,
    CreatedBy   VARCHAR(100)  NULL,
    CreatedOn   TIMESTAMP     NULL DEFAULT (now() AT TIME ZONE 'utc')
);
