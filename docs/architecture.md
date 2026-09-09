# Architecture

## Overview

```
+---------------------+        +---------------------+
|  Coms.Web           |        |  Coms.Desktop       |
|  ASP.NET Core MVC   |        |  WinForms           |
|  .NET 10            |        |  .NET Framework 4.8 |
+----------+----------+        +----------+----------+
           |                              |
           +---------------+--------------+
                           |
                +----------v----------+
                |  Coms.Application   |  use-case services,
                |  .NET Standard 2.0  |  workflow, CSV
                +----------+----------+
                           |
                +----------v----------+
                |  Coms.Domain        |  entities, enums,
                |  .NET Standard 2.0  |  validation, interfaces
                +----------+----------+
                           |
                +----------v----------+
                |  Coms.Data          |  Dapper repositories,
                |  .NET Standard 2.0  |  SQL text, connection factory
                +----------+----------+
                           |
                +----------v----------+
                |  SQL Server         |  schema owned by
                |  db/migrations      |  versioned T-SQL scripts
                +---------------------+
```

## Key decisions

### Two front ends, one core

The web application and the desktop client reference the same three shared
libraries. Business rules (order status transitions, pricing, numbering,
validation) live in `Coms.Domain` and `Coms.Application`, never in a
controller or a form. This is what makes the mixed-framework setup honest
rather than two separate applications that happen to share a database.

### .NET Standard 2.0 for shared code

.NET Standard 2.0 is the highest target consumable by both .NET Framework 4.8
and .NET 10. The constraint is real and shapes the code: no default interface
members, no `IAsyncEnumerable` in public APIs, no `Span<T>`-based APIs in
public signatures, and C# language features used must not require runtime
support that 4.8 lacks.

### Dapper instead of Entity Framework

Entity Framework Core 8+ requires .NET 8+, so it cannot be shared with the
4.8 client. Entity Framework 6 works on both but hides the SQL. Dapper keeps
the data layer shareable, keeps the SQL visible and reviewable, and makes
the database design a first-class deliverable rather than a by-product of
class definitions.

### Schema-first database with forward-only migrations

The database is defined by numbered T-SQL scripts in `db/migrations`,
applied by `tools/Coms.DbMigrator` (DbUp). Scripts are never edited after
they are committed; changes are new scripts. Reporting is done through views
and stored procedures where it belongs in the database, not in C# loops.

### Authentication

- Web: ASP.NET Core Identity with three roles: Administrator, Staff, ReadOnly.
- Desktop: internal-network tool. It records the Windows user name for audit
  and does not authenticate separately. This mirrors common practice for
  back-office clients and is documented as a deliberate scope decision.

### Concurrency

Every mutable table carries a `rowversion` column. Updates include the
expected version and fail with a clear message when the row changed
underneath the user. Both clients surface this the same way.

## Cross-cutting

- Logging: `Microsoft.Extensions.Logging` abstractions in shared code; the
  web host logs to console and rolling file, the desktop client to a rolling
  file under `%LOCALAPPDATA%`.
- Configuration: connection string in `appsettings.json` (web) and
  `App.config` (desktop); secrets via user secrets or environment variables.
- Validation: domain objects validate themselves; application services
  return a result object and never throw for business rule failures.
