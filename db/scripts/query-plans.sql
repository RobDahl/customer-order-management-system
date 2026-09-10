/* ---------------------------------------------------------------------------
   Query plans for the heaviest everyday queries
   Purpose : Show the estimated plan and the I/O for the two queries the
             application runs most: a page of the order list from
             rpt.vw_OrderSummary, and the receivables aging report. Run
             against the seeded database after any index change.
   Usage   : sqlcmd -S (localdb)\MSSQLLocalDB -d Coms -I -i db/scripts/query-plans.sql -W -w 300
   --------------------------------------------------------------------------- */

SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

PRINT '--- 1. Order list page: status filter, newest first, page 3 of 50 ---';
SET STATISTICS IO ON;
GO
SELECT s.OrderId, s.OrderNumber, s.Status, s.OrderDate, s.CustomerName, s.Total, s.LineCount, s.InvoiceNumber
  FROM rpt.vw_OrderSummary s
 WHERE s.Status = 'Approved'
 ORDER BY s.OrderDate DESC, s.OrderId DESC
OFFSET 100 ROWS FETCH NEXT 50 ROWS ONLY;

SELECT COUNT(*) FROM rpt.vw_OrderSummary s WHERE s.Status = 'Approved';
GO
SET STATISTICS IO OFF;
GO

PRINT '--- 2. Receivables aging ---';
SET STATISTICS IO ON;
GO
EXEC rpt.usp_ArAging @AsOfDate = '2026-09-01';
GO
SET STATISTICS IO OFF;
GO

PRINT '--- Estimated plans (text) ---';
SET SHOWPLAN_TEXT ON;
GO
SELECT s.OrderId, s.OrderNumber, s.Status, s.OrderDate, s.CustomerName, s.Total, s.LineCount, s.InvoiceNumber
  FROM rpt.vw_OrderSummary s
 WHERE s.Status = 'Approved'
 ORDER BY s.OrderDate DESC, s.OrderId DESC
OFFSET 100 ROWS FETCH NEXT 50 ROWS ONLY;
GO
EXEC rpt.usp_ArAging @AsOfDate = '2026-09-01';
GO
SET SHOWPLAN_TEXT OFF;
GO
