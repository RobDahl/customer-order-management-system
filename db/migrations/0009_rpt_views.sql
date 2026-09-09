/* ---------------------------------------------------------------------------
   0009  Reporting views
   Purpose : Read-only shapes used by list screens, detail pages and the
             credit check. Each view answers one question and joins only
             what that question needs.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

/* Order list and order detail header: order plus customer plus live invoice. */
CREATE OR ALTER VIEW rpt.vw_OrderSummary
AS
SELECT o.Id             AS OrderId,
       o.OrderNumber,
       o.Status,
       o.OrderDate,
       o.RequiredDate,
       o.CustomerReference,
       o.CustomerId,
       c.CustomerNumber,
       c.Name           AS CustomerName,
       o.Subtotal,
       o.TaxRate,
       o.TaxAmount,
       o.Total,
       ISNULL(l.LineCount, 0) AS LineCount,
       i.Id             AS InvoiceId,
       i.InvoiceNumber,
       i.Status         AS InvoiceStatus,
       o.CreatedAtUtc,
       o.CreatedBy,
       o.UpdatedAtUtc,
       o.UpdatedBy,
       o.RowVersion
  FROM dbo.Orders o
  JOIN dbo.Customers c ON c.Id = o.CustomerId
  OUTER APPLY (SELECT COUNT(*) AS LineCount FROM dbo.OrderLines ol WHERE ol.OrderId = o.Id) l
  LEFT JOIN dbo.Invoices i ON i.OrderId = o.Id AND i.Status <> 'Void';
GO

/* One row per customer with what they owe and what they still have room
   for. Used by the customer detail page and by the approve step's credit
   check. "Committed" is work approved or fulfilled but not yet invoiced. */
CREATE OR ALTER VIEW rpt.vw_CustomerBalance
AS
SELECT c.Id               AS CustomerId,
       c.CustomerNumber,
       c.Name,
       c.Status,
       c.PaymentTermsDays,
       c.CreditLimit,
       ISNULL(inv.OpenInvoiceCount, 0)   AS OpenInvoiceCount,
       ISNULL(inv.OutstandingBalance, 0) AS OutstandingBalance,
       ISNULL(inv.OverdueBalance, 0)     AS OverdueBalance,
       inv.OldestDueDate,
       ISNULL(ord.CommittedOrderCount, 0) AS CommittedOrderCount,
       ISNULL(ord.CommittedTotal, 0)      AS CommittedTotal,
       ISNULL(inv.OutstandingBalance, 0) + ISNULL(ord.CommittedTotal, 0) AS Exposure,
       CASE WHEN c.CreditLimit IS NULL THEN NULL
            ELSE c.CreditLimit - ISNULL(inv.OutstandingBalance, 0) - ISNULL(ord.CommittedTotal, 0)
       END AS CreditAvailable
  FROM dbo.Customers c
  LEFT JOIN
       (SELECT i.CustomerId,
               COUNT(*)                     AS OpenInvoiceCount,
               SUM(i.Total - i.AmountPaid)  AS OutstandingBalance,
               SUM(CASE WHEN i.DueDate < CAST(SYSUTCDATETIME() AS DATE) THEN i.Total - i.AmountPaid ELSE 0 END) AS OverdueBalance,
               MIN(i.DueDate)               AS OldestDueDate
          FROM dbo.Invoices i
         WHERE i.Status IN ('Open', 'PartiallyPaid')
         GROUP BY i.CustomerId) inv ON inv.CustomerId = c.Id
  LEFT JOIN
       (SELECT o.CustomerId,
               COUNT(*)     AS CommittedOrderCount,
               SUM(o.Total) AS CommittedTotal
          FROM dbo.Orders o
         WHERE o.Status IN ('Approved', 'Fulfilled')
         GROUP BY o.CustomerId) ord ON ord.CustomerId = c.Id;
GO

/* Active products at or below reorder level once open demand is taken
   into account. Demand counts submitted and approved orders, which have
   not yet moved stock. */
CREATE OR ALTER VIEW rpt.vw_LowStock
AS
SELECT p.Id            AS ProductId,
       p.Sku,
       p.Name,
       p.Category,
       p.UnitOfMeasure,
       p.QuantityOnHand,
       p.ReorderLevel,
       ISNULL(d.PendingDemand, 0)                        AS PendingDemand,
       p.QuantityOnHand - ISNULL(d.PendingDemand, 0)     AS ProjectedOnHand,
       p.ReorderLevel - (p.QuantityOnHand - ISNULL(d.PendingDemand, 0)) AS Shortfall
  FROM dbo.Products p
  LEFT JOIN
       (SELECT l.ProductId, SUM(l.Quantity) AS PendingDemand
          FROM dbo.OrderLines l
          JOIN dbo.Orders o ON o.Id = l.OrderId
         WHERE o.Status IN ('Submitted', 'Approved')
         GROUP BY l.ProductId) d ON d.ProductId = p.Id
 WHERE p.IsActive = 1
   AND p.QuantityOnHand - ISNULL(d.PendingDemand, 0) <= p.ReorderLevel;
GO

/* Open invoices with their age relative to today. The bucket boundaries
   match rpt.usp_ArAging so the list and the summary agree. */
CREATE OR ALTER VIEW rpt.vw_InvoiceAging
AS
SELECT i.Id            AS InvoiceId,
       i.InvoiceNumber,
       i.CustomerId,
       c.CustomerNumber,
       c.Name          AS CustomerName,
       i.OrderId,
       o.OrderNumber,
       i.IssuedDate,
       i.DueDate,
       i.Total,
       i.AmountPaid,
       i.Total - i.AmountPaid AS Balance,
       i.Status,
       CASE WHEN i.DueDate >= x.Today THEN 0 ELSE DATEDIFF(DAY, i.DueDate, x.Today) END AS DaysOverdue,
       CASE WHEN i.DueDate >= x.Today                          THEN 'Current'
            WHEN DATEDIFF(DAY, i.DueDate, x.Today) <= 30       THEN '1-30'
            WHEN DATEDIFF(DAY, i.DueDate, x.Today) <= 60       THEN '31-60'
            WHEN DATEDIFF(DAY, i.DueDate, x.Today) <= 90       THEN '61-90'
            ELSE '90+'
       END AS AgeBucket
  FROM dbo.Invoices i
  JOIN dbo.Customers c ON c.Id = i.CustomerId
  JOIN dbo.Orders o    ON o.Id = i.OrderId
  CROSS APPLY (SELECT CAST(SYSUTCDATETIME() AS DATE) AS Today) x
 WHERE i.Status IN ('Open', 'PartiallyPaid');
GO
