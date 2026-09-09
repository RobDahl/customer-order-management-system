/* ---------------------------------------------------------------------------
   Sample report calls
   Purpose : Exercise every reporting object against the seeded database.
             Handy for checking output shape after a schema change and for
             capturing execution plans.
   Usage   : sqlcmd -S (localdb)\MSSQLLocalDB -d Coms -i db/scripts/sample-reports.sql -W -w 200
   --------------------------------------------------------------------------- */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   /* required by the filtered indexes; sqlcmd defaults to OFF */

/* The seed anchors its dates to 2026-09-01; use the same window here. */
DECLARE @from DATE = '2025-09-01';
DECLARE @to   DATE = '2026-09-01';

PRINT '--- Dashboard counts ---';
EXEC rpt.usp_DashboardCounts;

PRINT '--- Sales by month ---';
EXEC rpt.usp_SalesByMonth @FromDate = @from, @ToDate = @to;

PRINT '--- Top 10 customers ---';
EXEC rpt.usp_TopCustomers @FromDate = @from, @ToDate = @to, @Top = 10;

PRINT '--- Receivables aging as at base date ---';
EXEC rpt.usp_ArAging @AsOfDate = '2026-09-01';

PRINT '--- Product sales, top 15 by revenue ---';
CREATE TABLE #ProductSales
(
    ProductId INT, Sku VARCHAR(40), Name NVARCHAR(200), Category NVARCHAR(60), UnitOfMeasure VARCHAR(10), IsActive BIT,
    OrderCount INT, QuantitySold DECIMAL(18,3), Revenue DECIMAL(18,2), Cost DECIMAL(18,2), GrossMargin DECIMAL(18,2), MarginPercent DECIMAL(5,2)
);
INSERT #ProductSales EXEC rpt.usp_ProductSales @FromDate = @from, @ToDate = @to;
SELECT TOP (15) Sku, Name, Category, OrderCount, QuantitySold, Revenue, GrossMargin, MarginPercent FROM #ProductSales ORDER BY Revenue DESC;
DROP TABLE #ProductSales;

PRINT '--- Low stock (first 15) ---';
SELECT TOP (15) Sku, Name, QuantityOnHand, PendingDemand, ProjectedOnHand, ReorderLevel, Shortfall
  FROM rpt.vw_LowStock
 ORDER BY Shortfall DESC;

PRINT '--- Customers over their credit limit ---';
SELECT CustomerNumber, Name, CreditLimit, OutstandingBalance, CommittedTotal, Exposure, CreditAvailable
  FROM rpt.vw_CustomerBalance
 WHERE CreditAvailable < 0
 ORDER BY CreditAvailable;

PRINT '--- Oldest open invoices ---';
SELECT TOP (10) InvoiceNumber, CustomerName, DueDate, Balance, DaysOverdue, AgeBucket
  FROM rpt.vw_InvoiceAging
 ORDER BY DaysOverdue DESC;

PRINT '--- Order status distribution ---';
SELECT Status, COUNT(*) AS Orders, SUM(Total) AS Value
  FROM dbo.Orders
 GROUP BY Status
 ORDER BY CASE Status WHEN 'Draft' THEN 1 WHEN 'Submitted' THEN 2 WHEN 'Approved' THEN 3
                      WHEN 'Fulfilled' THEN 4 WHEN 'Invoiced' THEN 5 ELSE 6 END;
