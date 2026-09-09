# Customer Order Management System

A line-of-business application for managing customers, products, orders,
invoices and payments. It models the kind of internal operations software
found in distribution, manufacturing, print, service and retail companies:
a SQL Server database at the centre, a web dashboard for managers and staff,
and a desktop client for back-office order entry.

The project exists to demonstrate practical business application development
on the Microsoft stack, including the mixed-framework reality of most
established .NET shops: a modern ASP.NET Core web application and a
.NET Framework 4.8 WinForms client sharing one domain model and one database.

## Stack

| Layer          | Technology                                              | Target             |
| -------------- | ------------------------------------------------------- | ------------------ |
| Database       | SQL Server 2022 / LocalDB, versioned SQL scripts (DbUp) | T-SQL              |
| Domain         | C# class library                                        | .NET Standard 2.0  |
| Data access    | Dapper + Microsoft.Data.SqlClient                       | .NET Standard 2.0  |
| Application    | Services, workflow, CSV import/export                   | .NET Standard 2.0  |
| Web            | ASP.NET Core MVC, ASP.NET Core Identity                 | .NET 10            |
| Desktop        | Windows Forms                                           | .NET Framework 4.8 |
| Tests          | xUnit                                                   | .NET 10            |
| CI             | GitHub Actions (build, test, SQL migrations)            |                    |

## Repository layout

```
src/
  Coms.Domain/          Entities, enums, validation, repository interfaces
  Coms.Data/            Dapper repositories, connection factory, SQL
  Coms.Application/     Use-case services: orders, invoicing, pricing, CSV
  Coms.Web/             ASP.NET Core MVC application (dashboard, admin)
  Coms.Desktop/         WinForms back-office client (.NET Framework 4.8)
tests/
  Coms.Domain.Tests/
  Coms.Application.Tests/
  Coms.Data.Tests/      Integration tests against a real SQL Server
  Coms.Web.Tests/
tools/
  Coms.DbMigrator/      Console runner that applies db/migrations in order
db/
  migrations/           Numbered, forward-only T-SQL scripts
  seed/                 Demo/reference data
  scripts/              Ad-hoc maintenance and reporting queries
docs/                   Architecture notes, UI style guide, screenshots
```

## Getting started

Prerequisites: .NET 10 SDK, Visual Studio 2026 (with the .NET desktop
workload for the WinForms client), SQL Server 2022 or SQL Server Express
LocalDB.

```
dotnet run --project tools/Coms.DbMigrator -- --seed
dotnet run --project src/Coms.Web
```

Then open the desktop client from Visual Studio (`src/Coms.Desktop`).

Detailed setup, configuration and architecture notes live in [docs/](docs/).

## Status

Under active development. See [docs/architecture.md](docs/architecture.md)
for design decisions, [docs/database.md](docs/database.md) for the schema,
numbering, workflow procedures and reporting objects, and
[docs/ui-style-guide.md](docs/ui-style-guide.md) for the interface
conventions.

## License

MIT. See [LICENSE](LICENSE).
