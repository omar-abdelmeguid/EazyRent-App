EazyRent Revamp (WinForms prototype)

This is a small WinForms prototype that demonstrates a frontend wired to backend methods and an ODBC-based data access layer to an Access database.

Requirements
- .NET 6 SDK (or newer) to build
- Microsoft Access ODBC driver installed for .mdb/.accdb (often part of 'Microsoft Access Database Engine')

How to build
1. Open PowerShell and navigate to this folder.
2. Run: dotnet build

How to run
1. dotnet run --project EazyRentRevamp.csproj
2. Or use the included run.ps1 script.

Notes
- This prototype uses ODBC. You'll set the DB file at runtime using the "Change DB" button.
- Replace the sample SQL in `BackendService.FetchRecords` with your production queries.
