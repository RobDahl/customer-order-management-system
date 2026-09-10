# Testing

## Layout

| Project | Kind | Runs against | Count |
| ------- | ---- | ------------ | ----- |
| `tests/Coms.Domain.Tests` | Unit | Nothing | 75 |
| `tests/Coms.Application.Tests` | Unit with in-memory fakes | Nothing | 85 |
| `tests/Coms.Data.Tests` | Integration | SQL Server database `Coms_Test` | 86 |
| `tests/Coms.Web.Tests` | In-process HTTP | SQL Server database `Coms_WebTest` | 51 |
| Desktop client | Manual checklist | LocalDB `Coms` | see below |

All automated tests use xUnit and plain `Assert`. Names follow
`Method_Scenario_Expectation`.

## Running

```
dotnet test CustomerOrderManagement.sln
```

The two database projects rebuild their database from scratch at the
start of every run: drop, create, apply every script in `db/migrations`,
load `db/seed`. That takes a few seconds on LocalDB and guarantees each
run sees the same data. They use different database names so they can run
side by side.

| Variable | Used by | Default |
| -------- | ------- | ------- |
| `COMS_TEST_CONNECTION` | Data tests | `(localdb)\MSSQLLocalDB`, database `Coms_Test` |
| `COMS_WEBTEST_CONNECTION` | Web tests | `(localdb)\MSSQLLocalDB`, database `Coms_WebTest` |

On a non-Windows machine without one of these variables the database
tests return early instead of failing. In CI they point at a SQL Server
container (see `.github/workflows/ci.yml`).

## What each layer covers

**Domain**: validation rules, the order status machine, money rounding,
the `Result` type, paging arithmetic. No I/O.

**Application**: every business rule in the services (submit, approve
with credit check, fulfil, cancel, invoice, payment, void, CSV import and
export) against fakes that mirror the stored procedures, including stock
movement. These fakes caught a real bug the SQL layer would have hidden:
the importer mutated loaded entities before the file had validated.

**Data**: each repository method against a real database, inside a
transaction that is rolled back when the test ends, so the seed data
never changes between tests. Stored procedure failure paths run without a
transaction because the procedure's own rollback would doom it. Also
checks the schema object list and seed row counts.

**Web**: the site hosted in-process with `WebApplicationFactory`, signing
in through the real login form (anti-forgery token included). Covers
role enforcement, validation round trips, the full order flow through the
forms (create, submit, approve, fulfil, invoice, pay, void), CSV import
upload, user administration and the health endpoint.

**Desktop**: no UI automation. Run the manual checklist after changes to
`src/Coms.Desktop`; it walks every screen and the same workflow.

## Conventions

- One assert concept per test; several `Assert` calls on one object are
  fine.
- Database tests that write open a session with `BeginSessionAsync()` and
  dispose it without committing.
- Web tests pick seed rows by query (`ScalarAsync`) rather than by fixed
  id, because earlier tests in the same run change stock and statuses.
- Warnings are errors and `dotnet format --verify-no-changes` runs in CI;
  a test project is held to the same rules as production code.
