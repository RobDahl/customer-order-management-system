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

### Data access shape

- `IDbSession` (implemented by `DbSession`) holds one connection and, when
  asked, one transaction. It is scoped per web request or per desktop
  action and also implements the domain's `IUnitOfWork`, so an application
  service can wrap several repository calls in one transaction without
  knowing anything about ADO.NET.
- Repositories take the session, write explicit SQL, and return domain
  entities or `null`. List queries return `PagedResult<T>` and accept a
  logical sort key that is mapped through a per-repository whitelist, so
  no user input reaches an `ORDER BY`.
- Updates carry the row's `rowversion` in the `WHERE` clause and return
  `false` when no row matched; the service turns that into a conflict
  result for the user.
- Calls to the workflow stored procedures return `Result` directly: the
  `THROW` numbers in the 50000 range are mapped to `ErrorCode` values, and
  anything else is a genuine fault that propagates.
- Enums are stored as their names. Parameters are passed as strings
  explicitly; Dapper maps the string columns back to enums on read.

### Application services

`Coms.Application` holds one service per area (customers, products, orders,
invoices, notes, reports, CSV import, CSV export), each behind an
interface. A service:

- normalises and validates input, returning `Result` with field-level
  errors rather than throwing;
- enforces the business rules that span records: the order state machine,
  customer status, product status, the credit check on approval, payment
  against balance, void only when unpaid;
- checks the caller's `rowversion` before changing anything and reports a
  conflict when it is stale;
- writes an audit line through `ILogger` for every state change.

Orders are built from `OrderInput`, which is what the screen collects.
The service looks up each product, snapshots SKU, description, price and
cost onto the line, applies the configured tax rate and computes totals.
The same code path serves "preview totals" without saving.

The CSV importer reads every row into all-string row objects, reports each
bad cell with its row number and field, and only when the whole file is
clean writes everything inside one unit of work. Exports use the same row
layout, so an exported file imports unchanged.

### Desktop client

`Coms.Desktop` targets .NET Framework 4.8 and C# 7.3 and references the
same three shared libraries as the web host. It builds its own dependency
injection container at start-up (`AddComsData`, `AddComsApplication`) and
runs every screen action inside a scope, so a list refresh or a save gets
one connection and one optional transaction, exactly like a web request.

- One main window with menu, toolbar and status bar hosts a list screen
  in a panel; records open as modal dialogs. No MDI.
- `ListScreen<T>` is the single list layout (filters, grid, pager,
  Refresh / New / Open / Export CSV); each module subclasses it with its
  columns and filter controls.
- Forms are built in code with `TableLayoutPanel` and standard controls;
  no custom drawing and no third-party control suite.
- Order entry edits lines in a `DataGridView` bound to plain row objects
  and recomputes totals with the domain's own `Order.Recalculate`, so the
  screen shows the same figures the server will store.
- Business results come back as `Result`; a validation failure lists its
  fields, a concurrency conflict reloads the record.
- Printing is text: documents are built as fixed-width lines and sent to
  a `PrintDocument` with a monospace font and a page footer.
- Logging goes through `Microsoft.Extensions.Logging` to a small rolling
  file provider under `%LOCALAPPDATA%\Coms\logs`.

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
