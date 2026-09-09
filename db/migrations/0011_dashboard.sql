/* ---------------------------------------------------------------------------
   0011  Dashboard counts
   Purpose : Everything the landing page shows in one round trip: one row,
             one column per figure. Conditional aggregation keeps each
             table to a single pass.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

CREATE OR ALTER PROCEDURE rpt.usp_DashboardCounts
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @today DATE = CAST(SYSUTCDATETIME() AS DATE);

    SELECT o.DraftOrders,
           o.SubmittedOrders,
           o.ApprovedOrders,
           o.FulfilledOrders,
           o.OrdersToday,
           o.OrdersTodayValue,
           i.OpenInvoices,
           i.OpenInvoiceBalance,
           i.OverdueInvoices,
           i.OverdueBalance,
           p.LowStockProducts,
           c.ActiveCustomers,
           c.OnHoldCustomers,
           @today AS AsOfDate
      FROM
           (SELECT COUNT(CASE WHEN Status = 'Draft'     THEN 1 END) AS DraftOrders,
                   COUNT(CASE WHEN Status = 'Submitted' THEN 1 END) AS SubmittedOrders,
                   COUNT(CASE WHEN Status = 'Approved'  THEN 1 END) AS ApprovedOrders,
                   COUNT(CASE WHEN Status = 'Fulfilled' THEN 1 END) AS FulfilledOrders,
                   COUNT(CASE WHEN OrderDate = @today AND Status <> 'Cancelled' THEN 1 END) AS OrdersToday,
                   ISNULL(SUM(CASE WHEN OrderDate = @today AND Status <> 'Cancelled' THEN Total END), 0) AS OrdersTodayValue
              FROM dbo.Orders) o
     CROSS JOIN
           (SELECT COUNT(*)                                              AS OpenInvoices,
                   ISNULL(SUM(Total - AmountPaid), 0)                   AS OpenInvoiceBalance,
                   COUNT(CASE WHEN DueDate < @today THEN 1 END)         AS OverdueInvoices,
                   ISNULL(SUM(CASE WHEN DueDate < @today THEN Total - AmountPaid END), 0) AS OverdueBalance
              FROM dbo.Invoices
             WHERE Status IN ('Open', 'PartiallyPaid')) i
     CROSS JOIN
           (SELECT COUNT(*) AS LowStockProducts FROM rpt.vw_LowStock) p
     CROSS JOIN
           (SELECT COUNT(CASE WHEN Status = 'Active' THEN 1 END) AS ActiveCustomers,
                   COUNT(CASE WHEN Status = 'OnHold' THEN 1 END) AS OnHoldCustomers
              FROM dbo.Customers) c;
END
GO
