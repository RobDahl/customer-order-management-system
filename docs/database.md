# Database

SQL Server 2019 or later (LocalDB is fine for development). The schema is
defined entirely by the numbered scripts in `db/migrations`, applied in
order by `tools/Coms.DbMigrator` and recorded in `dbo.SchemaVersions`.
Scripts are never edited once committed; a change is a new script.

## Entity relationship overview

```
 dbo.Customers 1 ------< dbo.Orders 1 ------< dbo.OrderLines >------ 1 dbo.Products
       |                     |
       |                     +------< dbo.OrderStatusHistory
       |                     |
       |                     +------  1 dbo.Invoices 1 ------< dbo.Payments
       |                                    |
       +------------------------------------+   (Invoices.CustomerId, denormalised)

 dbo.Notes  (EntityType + EntityId points at a Customer, Order or Invoice)
 dbo.NumberSeries, dbo.seq_CustomerNumber   (document numbering)
 auth.*     (ASP.NET Core Identity, created by the web project)
 rpt.*      (views and procedures for reporting)
```

## Schemas

| Schema | Holds | Owner of changes |
| ------ | ----- | ---------------- |
| `dbo`  | Business tables, workflow and numbering procedures | `db/migrations` |
| `rpt`  | Reporting views and procedures | `db/migrations` |
| `auth` | ASP.NET Core Identity tables | EF Core migration inside `Coms.Web` (the only place EF is used) |

## Tables

### Conventions

- `Id INT IDENTITY` clustered primary key on every table.
- Mutable tables carry `CreatedAtUtc`, `CreatedBy`, `UpdatedAtUtc`,
  `UpdatedBy` and a `ROWVERSION` column used for optimistic concurrency.
  Append-only tables (`OrderStatusHistory`, `Notes`) carry only the
  "created" pair, since their rows are never updated.
- Money is `DECIMAL(18,2)`, quantities `DECIMAL(18,3)`, rates `DECIMAL(9,6)`.
- Statuses are `VARCHAR` with `CHECK` constraints rather than lookup
  tables. There are few of them, they never change without a code change,
  and they map directly to C# enums.
- Every constraint and index is named: `PK_`, `FK_Child_Parent`,
  `CK_Table_Rule`, `DF_Table_Column`, `UX_`/`IX_Table_Columns`.

### dbo.Customers

Master record with billing address, optional shipping address (all-or-
nothing, enforced by `CK_Customers_ShippingAddress`), payment terms in
days, optional credit limit and a status of `Active`, `OnHold` or
`Inactive`.

### dbo.Products

Master record with SKU (stored upper-case, unique), price, cost, unit of
measure, quantity on hand and reorder level. Stock is a single number per
product; there is no warehouse or location dimension.

### dbo.Orders / dbo.OrderLines

The order header stores its totals rather than deriving them, so lists and
reports never aggregate lines. Lines snapshot the SKU, description, unit
price and unit cost at the time they were added; later product edits do
not change historical orders. The ship-to address is likewise a snapshot.

Status is one of `Draft`, `Submitted`, `Approved`, `Fulfilled`, `Invoiced`,
`Cancelled`. Allowed transitions are enforced by the application layer
(and by the procedures for the transitions they own):

```
Draft -> Submitted -> Approved -> Fulfilled -> Invoiced
  |          |            |
  +----------+------------+--------> Cancelled
```

### dbo.OrderStatusHistory

One row per transition, including the initial `NULL -> Draft`. Written by
whichever component performs the transition.

### dbo.Invoices / dbo.Payments

One live invoice per order, enforced by the filtered unique index
`UX_Invoices_OrderId_Live` (`WHERE Status <> 'Void'`), so a voided invoice
stays on file and the order can be re-invoiced. `AmountPaid` is maintained
by `usp_Invoice_ApplyPayment`; `CK_Invoices_Status_AmountPaid` guarantees
that `Status` (`Open`, `PartiallyPaid`, `Paid`, `Void`) always agrees with
it.

### dbo.Notes

Free-text notes against a customer, order or invoice. No foreign key
because the target is one of three tables; the application checks the
target exists before inserting.

## Document numbering

Public-facing numbers are generated in the database, never in C#, so both
clients and any future import get the same result.

| Number | Format | Source |
| ------ | ------ | ------ |
| Customer | `CUST-000123` | `dbo.seq_CustomerNumber` via `usp_NextCustomerNumber` |
| Order | `ORD-2026-000042` | `dbo.NumberSeries` via `usp_NextOrderNumber` |
| Invoice | `INV-2026-000017` | `dbo.NumberSeries` via `usp_NextInvoiceNumber` |

Order and invoice numbers restart each year. `NumberSeries` holds one row
per series and year; `usp_NextSeriesNumber` takes an update lock with a
range lock on that row inside a transaction, which serialises concurrent
callers and lets the first caller of a new year insert the row without an
error path. Gaps can occur when a transaction rolls back after taking a
number; that is normal and accepted.

## Workflow procedures

Transitions that touch several tables run as stored procedures with
`XACT_ABORT ON`, so any failure rolls the whole step back:

| Procedure | Does |
| --------- | ---- |
| `usp_Order_Fulfil` | Checks status `Approved`, checks stock per product (unless `@AllowNegativeStock = 1`), decrements `QuantityOnHand`, sets `Fulfilled`, writes history |
| `usp_Invoice_CreateFromOrder` | Checks status `Fulfilled` and no live invoice, takes an invoice number, copies totals, due date = issued + customer terms, sets order `Invoiced`, writes history |
| `usp_Invoice_ApplyPayment` | Checks the invoice is open and the amount does not exceed the balance, inserts the payment, updates `AmountPaid` and `Status` |
| `usp_Invoice_Void` | Refuses if any payment exists, sets `Void`, returns the order to `Fulfilled`, writes history |

Each accepts an optional `@RowVersion`; if supplied and stale the call
fails with error 50010, giving procedure calls the same optimistic
concurrency behaviour as ordinary updates.

Simpler transitions (`Submit`, `Approve`, `Cancel`) are single-row updates
performed by the application layer, which also runs the credit check on
approval using `rpt.vw_CustomerBalance`.

### Error numbers

Raised with `THROW` and mapped to result codes in `Coms.Data`:

| Number | Meaning |
| ------ | ------- |
| 50000 | Number series exhausted |
| 50001 | Order not found |
| 50002 | Order not in the required status |
| 50003 | Insufficient stock (message lists the SKUs) |
| 50004 | Invoice not found |
| 50005 | Live invoice already exists for the order |
| 50006 | Invoice not open |
| 50007 | Payment exceeds the outstanding balance |
| 50008 | Invoice has payments and cannot be voided |
| 50009 | Order has no lines |
| 50010 | Row modified by another user |
| 50020 | Invalid report parameters |

## Reporting objects

| Object | Purpose |
| ------ | ------- |
| `rpt.vw_OrderSummary` | Order list and header: order, customer, line count, live invoice |
| `rpt.vw_CustomerBalance` | Per customer: open invoices, outstanding and overdue balance, committed (approved/fulfilled, uninvoiced) order value, credit available |
| `rpt.vw_LowStock` | Active products at or below reorder level after subtracting open demand |
| `rpt.vw_InvoiceAging` | Open invoices with days overdue and age bucket |
| `rpt.usp_SalesByMonth` | Monthly order count and value; months with no sales are returned as zeros |
| `rpt.usp_TopCustomers` | Customers ranked by sales in a period with share of total |
| `rpt.usp_ArAging` | Receivables aging per customer: not yet due, 1-30, 31-60, 61-90, over 90 |
| `rpt.usp_ProductSales` | Quantity, revenue, cost and margin per product for a period |
| `rpt.usp_DashboardCounts` | One row of counts and totals for the landing page |

"Sales" means orders in `Fulfilled` or `Invoiced` status by order date.

## Indexes

Each index exists for a named query. The main ones:

| Index | Serves |
| ----- | ------ |
| `IX_Customers_Name` | Customer search by name prefix |
| `IX_Products_Active_Stock` (filtered `IsActive = 1`) | Low stock report |
| `IX_Orders_CustomerId (CustomerId, OrderDate DESC)` | Customer detail: their orders |
| `IX_Orders_Status_OrderDate` | Order list filtered by status; dashboard counts |
| `IX_Orders_OrderDate` | Date-range reports |
| `IX_OrderLines_ProductId` | Product sales report; open demand in low stock |
| `IX_Invoices_Status_DueDate` | Overdue and aging queries |
| `IX_Invoices_CustomerId` | Customer statement |
| `IX_Notes_Entity` | Notes for one entity, newest first |

`db/scripts/index-usage.sql` reports seeks, scans and updates per index and
lists missing-index suggestions from the optimiser.

## Seed data

`db/seed/demo.sql` generates a deterministic two-year history: 200
customers, 150 products, 5,000 orders (about 17,500 lines), status history,
about 4,000 invoices and 3,500 payments, and a few hundred notes. Dates are
anchored to 2026-09-01 so reports are reproducible. The script is a no-op
when customers already exist; use `--drop --seed` to regenerate.

## Useful scripts

| Script | Purpose |
| ------ | ------- |
| `db/scripts/sample-reports.sql` | Runs every reporting object against the seed data |
| `db/scripts/workflow-smoke-test.sql` | Drives one order through the workflow procedures, checking each expected failure, then restores the rows |
| `db/scripts/index-usage.sql` | Index usage statistics and missing-index suggestions |
