# Head Lending Visit Report API

.NET 8 Web API backend for the "Visit Report of Head (Lending)" form. Pure ADO.NET
(**Npgsql**) — no EF Core, no ORM. Database: **PostgreSQL 10.3**.

The project was migrated from SQL Server. PostgreSQL 10 has no `CREATE PROCEDURE`
(added in v11), so every former stored procedure is a **PostgreSQL function with the
same name** (`dbo.usp_VisitReport_Create`, `dbo.usp_Attachment_Insert`, ...). The API
calls them with `SELECT * FROM dbo.fn(@p1, @p2, ...)`.

* **Table-Valued Parameters → JSONB.** The dynamic row-sections are still built as
  DataTables, then sent as a JSONB array and expanded with `jsonb_to_recordset`.
  A plpgsql function runs in one transaction, so a whole report is still written
  all-or-nothing in one round trip.
* **Multiple result sets → batched calls.** Procedures that returned several result
  sets (GetById, the paged lists + `TotalCount`, login profile) are now several
  functions sent in one batched command, so `reader.NextResultAsync()` logic is unchanged.
* SQL Server's case-insensitive `LIKE` became `ILIKE`; e-mails compare with `LOWER()`.

## 1. Create the database objects

Run the scripts in `Scripts/PostgreSQL/` **in order** with `psql`:

```bash
psql -h <host> -U <user> -d <database> -f Scripts/PostgreSQL/001_CreateTables.sql
psql -h <host> -U <user> -d <database> -f Scripts/PostgreSQL/002_MasterTables.sql   # skip if you migrate the ERP master tables yourself
psql -h <host> -U <user> -d <database> -f Scripts/PostgreSQL/003_VisitReportFunctions.sql
psql -h <host> -U <user> -d <database> -f Scripts/PostgreSQL/004_AttachmentFunctions.sql
psql -h <host> -U <user> -d <database> -f Scripts/PostgreSQL/005_ListingAndAdminFunctions.sql
```

Everything lives in a `dbo` schema so object names match the old ones. The original
SQL Server scripts are still in `Scripts/` for reference.

## 2. Configure the connection

The connection string is built in `Data/PostgresConnectionFactory.cs`
(`GetPostgresConnectionString`) from the encrypted `Credntials` section of
`appsettings.json`, the same pattern as PDL.ManagementService:

```json
"Credntials": {
  "HostIp": "<server ip>",
  "Port": "5432",
  "Username": "<encrypted>",
  "Password": "<encrypted>",
  "Database": "<encrypted>"
},
"encryptSalts": { "userSalt": "<salt>", "dbSalt": "<salt>" }
```

`Username` is decrypted with `userSalt`; `Password` and `Database` with `dbSalt`
(`Helpers/EncryptionHelper.Decrypt`).

## 3. Run the API

```bash
dotnet restore
dotnet run
```

Swagger UI opens at `/swagger` and lists every endpoint below.

> **Note:** the PostgreSQL scripts were executed and smoke-tested, but the C# was not
> compiled in the migration environment — run `dotnet build` locally first.

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
path is stored in PostgreSQL — `FileStorageService` is the only place that resolves
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
