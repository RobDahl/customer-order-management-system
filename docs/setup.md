# Setup

## Prerequisites

| Requirement | Notes |
| ----------- | ----- |
| .NET 10 SDK | `dotnet --version` should print 10.0.x. `global.json` pins the major version. |
| SQL Server  | Any of: SQL Server 2019+ (Express is fine), SQL Server Express LocalDB (installed with Visual Studio), or a container. |
| Visual Studio 2026 | Only needed to run and debug the WinForms client. Install the ".NET desktop development" workload. Not required for the web application, tests or migrator. |
| Git | |

## 1. Clone

```
git clone <repository-url>
cd customer-order-management-system
```

## 2. Configure the connection string

The default everywhere is LocalDB:

```
Server=(localdb)\MSSQLLocalDB;Database=Coms;Integrated Security=true;TrustServerCertificate=true
```

To use a different server, set it in one place per host:

| Host | Where |
| ---- | ----- |
| Migrator | `COMS_CONNECTION` environment variable, or `--connection "<string>"` |
| Web | `dotnet user-secrets set "ConnectionStrings:Coms" "<string>" --project src/Coms.Web`, or the `ConnectionStrings__Coms` environment variable |
| Desktop | `connectionStrings/add[@name='Coms']` in `src/Coms.Desktop/App.config` |
| Data tests | `COMS_TEST_CONNECTION` environment variable (defaults to LocalDB database `Coms_Test`) |

## 3. Create the database

```
dotnet run --project tools/Coms.DbMigrator -- --seed
```

This creates the database if it does not exist, applies every script in
`db/migrations` that has not yet been recorded in `dbo.SchemaVersions`,
then runs the re-runnable scripts in `db/seed`.

Other useful invocations:

```
dotnet run --project tools/Coms.DbMigrator                       # migrations only
dotnet run --project tools/Coms.DbMigrator -- --drop --seed      # start over (development only)
dotnet run --project tools/Coms.DbMigrator -- --help
```

## 4. Run the web application

```
dotnet run --project src/Coms.Web
```

Browse to <https://localhost:7180>. On first start the application creates
the Identity tables in the `auth` schema and, when no users exist yet, the
seed users from `appsettings.json`:

| User     | Password      | Role          | Can |
| -------- | ------------- | ------------- | --- |
| `admin`  | `Admin#2026`  | Administrator | Everything, including user administration |
| `staff`  | `Staff#2026`  | Staff         | Create and change customers, products, orders, invoices |
| `viewer` | `Viewer#2026` | ReadOnly      | View and export only |

These are development defaults. For anything beyond a workstation, override
the `Identity:SeedUsers` section (user secrets or environment variables)
before the first start, or change the passwords after signing in.

## 5. Run the desktop client

Open `CustomerOrderManagement.sln` in Visual Studio, set `Coms.Desktop` as
the startup project and press F5. Alternatively build from the command line
and launch the executable:

```
dotnet build src/Coms.Desktop
src\Coms.Desktop\bin\Debug\net48\Coms.Desktop.exe
```

## 6. Run the tests

```
dotnet test CustomerOrderManagement.sln
```

The data integration tests use LocalDB by default and skip themselves when
no SQL Server is reachable on non-Windows machines. Point them at another
server with `COMS_TEST_CONNECTION`.

## Command reference

Everything above in one place. All commands run from the repository root.

| Task | Command |
| ---- | ------- |
| Create or update the database | `dotnet run --project tools/Coms.DbMigrator` |
| Create or update and load demo data | `dotnet run --project tools/Coms.DbMigrator -- --seed` |
| Reset the database to a clean seed (development only) | `dotnet run --project tools/Coms.DbMigrator -- --drop --seed` |
| Migrator options | `dotnet run --project tools/Coms.DbMigrator -- --help` |
| Run the web application | `dotnet run --project src/Coms.Web` then open <https://localhost:7180> |
| Build the desktop client | `dotnet build src/Coms.Desktop` then run `src\Coms.Desktop\bin\Debug\net48\Coms.Desktop.exe` |
| Build everything | `dotnet build CustomerOrderManagement.sln` |
| Run all tests | `dotnet test CustomerOrderManagement.sln` |
| Run one test project | `dotnet test tests/Coms.Domain.Tests` |
| Check formatting (what CI runs) | `dotnet format CustomerOrderManagement.sln --verify-no-changes` |
| Fix formatting | `dotnet format CustomerOrderManagement.sln` |
| Run every report against the seed data | `sqlcmd -S "(localdb)\MSSQLLocalDB" -d Coms -I -W -w 200 -i db/scripts/sample-reports.sql` |
| Exercise the workflow procedures | `sqlcmd -S "(localdb)\MSSQLLocalDB" -d Coms -I -i db/scripts/workflow-smoke-test.sql` |
| Index usage and missing-index suggestions | `sqlcmd -S "(localdb)\MSSQLLocalDB" -d Coms -I -W -w 200 -i db/scripts/index-usage.sql` |
| Ad-hoc query | `sqlcmd -S "(localdb)\MSSQLLocalDB" -d Coms -I -Q "SELECT TOP 5 * FROM rpt.vw_OrderSummary"` |

`-I` turns on `QUOTED_IDENTIFIER`, which the filtered indexes need. `-W`
trims padding and `-w 200` widens the output so report columns fit.

## Troubleshooting

- **LocalDB not found**: run `sqllocaldb info`. If `MSSQLLocalDB` is missing,
  create it with `sqllocaldb create MSSQLLocalDB -s`.
- **"Cannot open database Coms"** from a client, and the migrator then fails
  with **"Cannot create file ... Coms.mdf because it already exists"**: the
  LocalDB instance was recreated (a Visual Studio or SQL tools update can do
  this) and lost its registration of the database, but the files in your
  profile folder are intact. Reattach them:

  ```
  sqlcmd -S "(localdb)\MSSQLLocalDB" -Q "CREATE DATABASE Coms ON (FILENAME = 'C:\Users\<you>\Coms.mdf'), (FILENAME = 'C:\Users\<you>\Coms_log.ldf') FOR ATTACH;"
  ```

  Or, if the data does not matter, delete the two files and run the
  migrator with `--seed` again.
- **sqlcmd scripts fail with "incorrect settings: QUOTED_IDENTIFIER"**: the
  scripts in `db/scripts` set the option themselves; for ad-hoc statements
  pass `-I` to `sqlcmd`. Filtered indexes require it.
- **Certificate errors**: the connection strings include
  `TrustServerCertificate=true` for development. Remove it and install a
  proper certificate for anything beyond a workstation.
- **WinForms project fails to build on a machine without Visual Studio**:
  the project references `Microsoft.NETFramework.ReferenceAssemblies`, so
  the .NET SDK alone is enough. Running it still needs the .NET Framework
  4.8 runtime, which ships with Windows 10 and later.
