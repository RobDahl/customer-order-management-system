/* ---------------------------------------------------------------------------
   0010  Reporting procedures
   Purpose : Parameterised reports for the Reports section and CSV export.
             "Sales" means orders that reached Fulfilled or Invoiced;
             drafts, submissions, approvals and cancellations are not sales.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

/* Sales by calendar month. Months with no sales are still returned with
   zeros so a chart or table has no gaps. */
CREATE OR ALTER PROCEDURE rpt.usp_SalesByMonth
    @FromDate DATE,
    @ToDate   DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL OR @ToDate IS NULL OR @FromDate > @ToDate
        THROW 50020, 'A valid date range is required.', 1;

    DECLARE @firstMonth DATE = DATEFROMPARTS(YEAR(@FromDate), MONTH(@FromDate), 1);
    DECLARE @lastMonth  DATE = DATEFROMPARTS(YEAR(@ToDate),   MONTH(@ToDate),   1);

    WITH Months AS
    (
        SELECT @firstMonth AS MonthStart
        UNION ALL
        SELECT DATEADD(MONTH, 1, MonthStart)
          FROM Months
         WHERE MonthStart < @lastMonth
    ),
    Sales AS
    (
        SELECT DATEFROMPARTS(YEAR(o.OrderDate), MONTH(o.OrderDate), 1) AS MonthStart,
               COUNT(*)         AS OrderCount,
               SUM(o.Subtotal)  AS Subtotal,
               SUM(o.TaxAmount) AS TaxAmount,
               SUM(o.Total)     AS Total
          FROM dbo.Orders o
         WHERE o.Status IN ('Fulfilled', 'Invoiced')
           AND o.OrderDate >= @FromDate
           AND o.OrderDate <= @ToDate
         GROUP BY DATEFROMPARTS(YEAR(o.OrderDate), MONTH(o.OrderDate), 1)
    )
    SELECT m.MonthStart,
           FORMAT(m.MonthStart, 'yyyy-MM')     AS MonthLabel,
           ISNULL(s.OrderCount, 0)             AS OrderCount,
           ISNULL(s.Subtotal, 0)               AS Subtotal,
           ISNULL(s.TaxAmount, 0)              AS TaxAmount,
           ISNULL(s.Total, 0)                  AS Total,
           CASE WHEN ISNULL(s.OrderCount, 0) = 0 THEN 0
                ELSE CAST(s.Total / s.OrderCount AS DECIMAL(18,2))
           END                                 AS AverageOrderValue
      FROM Months m
      LEFT JOIN Sales s ON s.MonthStart = m.MonthStart
     ORDER BY m.MonthStart
    OPTION (MAXRECURSION 1200);
END
GO

/* Customers ranked by sales value in the period, with their share of the
   period total. */
CREATE OR ALTER PROCEDURE rpt.usp_TopCustomers
    @FromDate DATE,
    @ToDate   DATE,
    @Top      INT = 10
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL OR @ToDate IS NULL OR @FromDate > @ToDate
        THROW 50020, 'A valid date range is required.', 1;

    IF @Top IS NULL OR @Top < 1
        SET @Top = 10;

    WITH Sales AS
    (
        SELECT o.CustomerId,
               COUNT(*)         AS OrderCount,
               SUM(o.Total)     AS Total,
               MAX(o.OrderDate) AS LastOrderDate
          FROM dbo.Orders o
         WHERE o.Status IN ('Fulfilled', 'Invoiced')
           AND o.OrderDate >= @FromDate
           AND o.OrderDate <= @ToDate
         GROUP BY o.CustomerId
    )
    SELECT TOP (@Top)
           ROW_NUMBER() OVER (ORDER BY s.Total DESC, c.Name) AS Rank,
           c.Id             AS CustomerId,
           c.CustomerNumber,
           c.Name           AS CustomerName,
           c.Status,
           s.OrderCount,
           s.Total,
           CAST(s.Total / s.OrderCount AS DECIMAL(18,2))                       AS AverageOrderValue,
           CAST(100.0 * s.Total / NULLIF(SUM(s.Total) OVER (), 0) AS DECIMAL(5,2)) AS SharePercent,
           s.LastOrderDate
      FROM Sales s
      JOIN dbo.Customers c ON c.Id = s.CustomerId
     ORDER BY s.Total DESC, c.Name;
END
GO

/* Accounts receivable aging: one row per customer with an open balance,
   bucketed by days past due as at the given date. */
CREATE OR ALTER PROCEDURE rpt.usp_ArAging
    @AsOfDate DATE = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SET @AsOfDate = ISNULL(@AsOfDate, CAST(SYSUTCDATETIME() AS DATE));

    SELECT c.Id             AS CustomerId,
           c.CustomerNumber,
           c.Name           AS CustomerName,
           c.PaymentTermsDays,
           COUNT(*)         AS InvoiceCount,
           SUM(CASE WHEN i.DueDate >= @AsOfDate                        THEN i.Total - i.AmountPaid ELSE 0 END) AS NotYetDue,
           SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOfDate) BETWEEN 1  AND 30 THEN i.Total - i.AmountPaid ELSE 0 END) AS Days1To30,
           SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOfDate) BETWEEN 31 AND 60 THEN i.Total - i.AmountPaid ELSE 0 END) AS Days31To60,
           SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOfDate) BETWEEN 61 AND 90 THEN i.Total - i.AmountPaid ELSE 0 END) AS Days61To90,
           SUM(CASE WHEN DATEDIFF(DAY, i.DueDate, @AsOfDate) > 90            THEN i.Total - i.AmountPaid ELSE 0 END) AS Over90,
           SUM(i.Total - i.AmountPaid) AS TotalOutstanding,
           MIN(i.DueDate)              AS OldestDueDate
      FROM dbo.Invoices i
      JOIN dbo.Customers c ON c.Id = i.CustomerId
     WHERE i.Status IN ('Open', 'PartiallyPaid')
       AND i.IssuedDate <= @AsOfDate
     GROUP BY c.Id, c.CustomerNumber, c.Name, c.PaymentTermsDays
     ORDER BY TotalOutstanding DESC, c.Name;
END
GO

/* Quantity, revenue and margin per product for orders sold in the period.
   Cost uses the UnitCost snapshot on the line, not the current product
   cost, so margins are as they were at the time of sale. */
CREATE OR ALTER PROCEDURE rpt.usp_ProductSales
    @FromDate DATE,
    @ToDate   DATE
AS
BEGIN
    SET NOCOUNT ON;

    IF @FromDate IS NULL OR @ToDate IS NULL OR @FromDate > @ToDate
        THROW 50020, 'A valid date range is required.', 1;

    SELECT p.Id            AS ProductId,
           p.Sku,
           p.Name,
           p.Category,
           p.UnitOfMeasure,
           p.IsActive,
           COUNT(DISTINCT l.OrderId)                AS OrderCount,
           SUM(l.Quantity)                          AS QuantitySold,
           SUM(l.LineTotal)                         AS Revenue,
           CAST(SUM(l.Quantity * l.UnitCost) AS DECIMAL(18,2)) AS Cost,
           CAST(SUM(l.LineTotal) - SUM(l.Quantity * l.UnitCost) AS DECIMAL(18,2)) AS GrossMargin,
           CAST(CASE WHEN SUM(l.LineTotal) = 0 THEN 0
                     ELSE 100.0 * (SUM(l.LineTotal) - SUM(l.Quantity * l.UnitCost)) / SUM(l.LineTotal)
                END AS DECIMAL(5,2))                AS MarginPercent
      FROM dbo.OrderLines l
      JOIN dbo.Orders o   ON o.Id = l.OrderId
      JOIN dbo.Products p ON p.Id = l.ProductId
     WHERE o.Status IN ('Fulfilled', 'Invoiced')
       AND o.OrderDate >= @FromDate
       AND o.OrderDate <= @ToDate
     GROUP BY p.Id, p.Sku, p.Name, p.Category, p.UnitOfMeasure, p.IsActive
     ORDER BY Revenue DESC, p.Sku;
END
GO
