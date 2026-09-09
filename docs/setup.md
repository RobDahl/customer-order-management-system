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

Browse to <https://localhost:7180>.

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

## Troubleshooting

- **LocalDB not found**: run `sqllocaldb info`. If `MSSQLLocalDB` is missing,
  create it with `sqllocaldb create MSSQLLocalDB -s`.
- **Certificate errors**: the connection strings include
  `TrustServerCertificate=true` for development. Remove it and install a
  proper certificate for anything beyond a workstation.
- **WinForms project fails to build on a machine without Visual Studio**:
  the project references `Microsoft.NETFramework.ReferenceAssemblies`, so
  the .NET SDK alone is enough. Running it still needs the .NET Framework
  4.8 runtime, which ships with Windows 10 and later.
