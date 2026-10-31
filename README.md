# Head Lending Visit Report API

.NET 6 Web API backend for the "Visit Report of Head (Lending)" form. Pure ADO.NET
(`Microsoft.Data.SqlClient`) — no EF Core, no ORM. Every read/write goes through a
SQL Server stored procedure; dynamic row-sections (loan types, borrowers, audit
points, etc.) are sent to SQL Server as Table-Valued Parameters so a whole report,
however many rows the user added, is written in one round trip and one transaction.

## 1. Create the database objects

Run the scripts in `Scripts/` **in order** against your SQL Server instance
(SSMS, Azure Data Studio, or `sqlcmd`):

```bash
sqlcmd -S localhost -E -i Scripts/001_CreateTables.sql
sqlcmd -S localhost -E -i Scripts/002_CreateTableTypes.sql
sqlcmd -S localhost -E -i Scripts/003_VisitReportProcedures.sql
sqlcmd -S localhost -E -i Scripts/004_AttachmentProcedures.sql
```

This creates the `HeadLendingVisitReportDb` database, the six tables
(`LendingVisitReports` + 5 child tables + `LendingVisitAttachments`), the five
Table-Valued Parameter types, and all stored procedures.

## 2. Configure the connection string

Edit `appsettings.json` / `appsettings.Development.json`:

```json
"ConnectionStrings": {
  "DefaultConnection": "Server=localhost;Database=HeadLendingVisitReportDb;Trusted_Connection=True;TrustServerCertificate=True;"
}
```

Swap in SQL auth (`User Id=...;Password=...;`) if you're not using Windows/Entra
integrated auth.

## 3. Run the API

```bash
dotnet restore
dotnet run
```

Swagger UI opens at `/swagger` and lists every endpoint below.

> **Note:** this sandbox doesn't have the .NET SDK installed, so this project was
> hand-written and reviewed but not compiled here — build it locally with the
> .NET 6 SDK before relying on it.

## Endpoints

| Method | Route | Purpose |
|---|---|---|
| GET | `/api/visit-reports` | Paged list (`?page=&pageSize=&branchName=&employeeCode=&fromDate=&toDate=`) |
| GET | `/api/visit-reports/{id}` | Single report with all row-sections + attachments |
| POST | `/api/visit-reports` | Create a report (send whatever number of rows per section) |
| PUT | `/api/visit-reports/{id}` | Replace a report's header + all row-sections |
| DELETE | `/api/visit-reports/{id}` | Delete report, rows, attachment records, and files on disk |
| GET | `/api/visit-reports/{id}/attachments` | List files attached to a report |
| POST | `/api/visit-reports/{id}/attachments` | Upload one or more files (`multipart/form-data`, field name `files`) |
| GET | `/api/visit-reports/{id}/attachments/{attachmentId}/download` | Download a file |
| DELETE | `/api/visit-reports/{id}/attachments/{attachmentId}` | Delete a file (record + disk) |

## File storage

Uploaded files are saved under `App_Data/Uploads/{visitReportId}/{guid}_{originalName}`
(configurable via `FileStorage:RootPath` in `appsettings.json`). Only the relative
path is stored in SQL Server — `FileStorageService` is the only place that resolves
it to an absolute disk path, with a path-traversal guard.

## Request body shape (Create / Update)

```jsonc
{
  "branchName": "Karol Bagh Branch",
  "headName": "Rohit Sharma",
  "employeeCode": "EMP1042",
  "visitDateTime": "2026-07-28T11:30:00",
  "businessDevelopments": [
    { "rowOrder": 1, "typeOfLoan": "Home Loan", "budget": 5000000, "sanctioned": 3200000, "disbursed": 3000000 }
  ],
  "cashManagements": [
    { "rowOrder": 1, "particulars": "Cash shortage/Misappropriation", "observation": "No", "remarks": "" }
  ],
  "npaOdManagements": [
    { "rowOrder": 1, "particulars": "1 EMI Due", "amount": 12000, "recoveryMade": 12000, "remarks": "" }
  ],
  "npaBorrowers": [
    { "rowOrder": 1, "borrowerName": "Suresh Kumar", "accountNo": "AC1029", "amountRecovered": 5000, "remarks": "" }
  ],
  "demandForMonth": 500000, "collectionAmount": 460000, "shortfall": 40000, "collectionPercent": 92.0, "collectionRemarks": "",
  "concurrentAudits": [
    { "rowOrder": 1, "auditorObservation": "KYC documents pending for 3 accounts", "branchCompliance": "Correct", "remarks": "Rectified on visit" }
  ],
  "obsBusiness": "", "obsCash": "", "obsOd": "", "obsNpa": "", "obsCollection": "", "obsAudit": "", "obsOthers": "",
  "signatureName": "Rohit Sharma", "signatureDate": "2026-07-28"
}
```

`rowOrder` just needs to be sequential per section — it's how row order from the
form is preserved; add or remove as many entries per array as the user needs.
