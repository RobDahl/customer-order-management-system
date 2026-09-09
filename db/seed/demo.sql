/* ---------------------------------------------------------------------------
   Demo data
   Purpose : Populate an empty database with a realistic two-year history:
             200 customers, 150 products, 5,000 orders with lines and
             status history, invoices and payments, plus some notes.

             Everything is generated set-based from a numbers table with
             deterministic hashes, so two runs on two machines produce the
             same rows. Dates are anchored to a fixed base date rather than
             today so screenshots and reports are reproducible.

             Re-runnable: the script does nothing if customers already
             exist. To regenerate, run the migrator with --drop --seed.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

SET NOCOUNT ON;
SET XACT_ABORT ON;

IF EXISTS (SELECT 1 FROM dbo.Customers)
BEGIN
    PRINT 'Seed skipped: dbo.Customers already contains rows.';
    RETURN;
END

DECLARE @seedUser      NVARCHAR(100) = N'seed';
DECLARE @baseDate      DATE          = '2026-09-01';
DECLARE @taxRate       DECIMAL(9,6)  = 0.080000;
DECLARE @customerCount INT           = 200;
DECLARE @productCount  INT           = 150;
DECLARE @orderCount    INT           = 5000;
DECLARE @historyDays   INT           = 730;

BEGIN TRANSACTION;

/* ---------------------------------------------------------------------------
   Numbers and word lists
   --------------------------------------------------------------------------- */

CREATE TABLE #Numbers (n INT NOT NULL PRIMARY KEY);

INSERT #Numbers (n)
SELECT TOP (40000) ROW_NUMBER() OVER (ORDER BY (SELECT NULL))
  FROM sys.all_objects a
 CROSS JOIN sys.all_objects b;

CREATE TABLE #CompanyFirst (i INT NOT NULL PRIMARY KEY, w NVARCHAR(40) NOT NULL);
INSERT #CompanyFirst (i, w) VALUES
    (1, N'Northgate'), (2, N'Harbor'), (3, N'Summit'), (4, N'Ridgeway'), (5, N'Ironbridge'),
    (6, N'Meadowlark'), (7, N'Clearwater'), (8, N'Stonebrook'), (9, N'Pinecrest'), (10, N'Westfield'),
    (11, N'Copperline'), (12, N'Bluehill'), (13, N'Oakmont'), (14, N'Silverton'), (15, N'Redfern'),
    (16, N'Granite'), (17, N'Lakeside'), (18, N'Fairview'), (19, N'Kingsway'), (20, N'Ashford');

CREATE TABLE #CompanySecond (i INT NOT NULL PRIMARY KEY, w NVARCHAR(40) NOT NULL);
INSERT #CompanySecond (i, w) VALUES
    (1, N'Supply Co.'), (2, N'Industries'), (3, N'Manufacturing'), (4, N'Logistics'), (5, N'Print Works'),
    (6, N'Distribution'), (7, N'Engineering'), (8, N'Services'), (9, N'Trading'), (10, N'Fabrication');

CREATE TABLE #FirstNames (i INT NOT NULL PRIMARY KEY, w NVARCHAR(40) NOT NULL);
INSERT #FirstNames (i, w) VALUES
    (1, N'Alex'), (2, N'Jordan'), (3, N'Sam'), (4, N'Taylor'), (5, N'Morgan'), (6, N'Casey'),
    (7, N'Riley'), (8, N'Jamie'), (9, N'Drew'), (10, N'Quinn'), (11, N'Avery'), (12, N'Reese');

CREATE TABLE #LastNames (i INT NOT NULL PRIMARY KEY, w NVARCHAR(40) NOT NULL);
INSERT #LastNames (i, w) VALUES
    (1, N'Nguyen'), (2, N'Patel'), (3, N'Okafor'), (4, N'Lindqvist'), (5, N'Moreau'), (6, N'Kowalski'),
    (7, N'Haddad'), (8, N'Fernandez'), (9, N'Brennan'), (10, N'Sato'), (11, N'Mbeki'), (12, N'Walsh');

CREATE TABLE #Streets (i INT NOT NULL PRIMARY KEY, w NVARCHAR(60) NOT NULL);
INSERT #Streets (i, w) VALUES
    (1, N'Main St'), (2, N'Industrial Pkwy'), (3, N'Commerce Dr'), (4, N'Mill Rd'), (5, N'Depot Ave'),
    (6, N'Enterprise Way'), (7, N'Harbor Blvd'), (8, N'Foundry Ln'), (9, N'Railway St'), (10, N'Market Sq');

CREATE TABLE #Cities (i INT NOT NULL PRIMARY KEY, City NVARCHAR(80) NOT NULL, Region NVARCHAR(80) NOT NULL, Postal CHAR(3) NOT NULL);
INSERT #Cities (i, City, Region, Postal) VALUES
    (1, N'Portland', N'OR', '972'), (2, N'Denver', N'CO', '802'), (3, N'Austin', N'TX', '787'),
    (4, N'Columbus', N'OH', '432'), (5, N'Raleigh', N'NC', '276'), (6, N'Boise', N'ID', '837'),
    (7, N'Tucson', N'AZ', '857'), (8, N'Madison', N'WI', '537'), (9, N'Richmond', N'VA', '232'),
    (10, N'Omaha', N'NE', '681'), (11, N'Spokane', N'WA', '992'), (12, N'Albany', N'NY', '122'),
    (13, N'Reno', N'NV', '895'), (14, N'Tulsa', N'OK', '741'), (15, N'Lexington', N'KY', '405');

CREATE TABLE #Categories (i INT NOT NULL PRIMARY KEY, Prefix CHAR(3) NOT NULL, Name NVARCHAR(60) NOT NULL, Uom VARCHAR(10) NOT NULL);
INSERT #Categories (i, Prefix, Name, Uom) VALUES
    (1, 'FAS', N'Fasteners', 'BOX'), (2, 'ADH', N'Adhesives', 'EA'), (3, 'PKG', N'Packaging', 'CS'),
    (4, 'SAF', N'Safety', 'EA'), (5, 'TLS', N'Tools', 'EA'), (6, 'ELC', N'Electrical', 'EA'),
    (7, 'CLN', N'Cleaning', 'EA'), (8, 'OFF', N'Office', 'PK');

CREATE TABLE #Items (Cat INT NOT NULL, k INT NOT NULL, Name NVARCHAR(80) NOT NULL, PRIMARY KEY (Cat, k));
INSERT #Items (Cat, k, Name) VALUES
    (1, 0, N'Hex Bolt M8'), (1, 1, N'Hex Bolt M10'), (1, 2, N'Wood Screw 4x40'), (1, 3, N'Lock Washer M8'), (1, 4, N'Nylon Insert Nut M6'),
    (2, 0, N'Contact Adhesive'), (2, 1, N'Epoxy Resin Kit'), (2, 2, N'Wood Glue'), (2, 3, N'Thread Locker'), (2, 4, N'Silicone Sealant'),
    (3, 0, N'Corrugated Box'), (3, 1, N'Stretch Film Roll'), (3, 2, N'Bubble Wrap Roll'), (3, 3, N'Packing Tape'), (3, 4, N'Mailing Bag'),
    (4, 0, N'Safety Glasses'), (4, 1, N'Nitrile Gloves'), (4, 2, N'Hi-Vis Vest'), (4, 3, N'Ear Defenders'), (4, 4, N'Hard Hat'),
    (5, 0, N'Claw Hammer'), (5, 1, N'Screwdriver Set'), (5, 2, N'Tape Measure'), (5, 3, N'Utility Knife'), (5, 4, N'Adjustable Wrench'),
    (6, 0, N'Cable Ties'), (6, 1, N'Insulation Tape'), (6, 2, N'Extension Lead'), (6, 3, N'Junction Box'), (6, 4, N'LED Work Light'),
    (7, 0, N'Degreaser'), (7, 1, N'Floor Cleaner'), (7, 2, N'Paper Towel Roll'), (7, 3, N'Microfibre Cloth'), (7, 4, N'Waste Bags'),
    (8, 0, N'Copy Paper A4'), (8, 1, N'Ballpoint Pens'), (8, 2, N'Label Sheets'), (8, 3, N'Ring Binder'), (8, 4, N'Sticky Notes');

CREATE TABLE #Variants (i INT NOT NULL PRIMARY KEY, w NVARCHAR(40) NOT NULL);
INSERT #Variants (i, w) VALUES (0, N'Standard'), (1, N'Heavy Duty'), (2, N'Economy'), (3, N'Bulk');

CREATE TABLE #NoteTexts (i INT NOT NULL PRIMARY KEY, w NVARCHAR(200) NOT NULL);
INSERT #NoteTexts (i, w) VALUES
    (0, N'Customer prefers delivery before 10:00.'),
    (1, N'Confirmed pricing by phone.'),
    (2, N'Requested split shipment; agreed to ship complete instead.'),
    (3, N'Account contact changed, see updated details.'),
    (4, N'Chased overdue balance, promised payment next week.'),
    (5, N'Goods collected by customer courier.'),
    (6, N'Backorder on one line, customer informed.'),
    (7, N'Credit limit reviewed with finance.');

/* ---------------------------------------------------------------------------
   Customers
   --------------------------------------------------------------------------- */

CREATE TABLE #Cust
(
    n  INT NOT NULL PRIMARY KEY,
    h1 INT NOT NULL, h2 INT NOT NULL, h3 INT NOT NULL, h4 INT NOT NULL
);

INSERT #Cust (n, h1, h2, h3, h4)
SELECT n,
       ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('cust-a', n)))) % 1000000,
       ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('cust-b', n)))) % 1000000,
       ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('cust-c', n)))) % 1000000,
       ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('cust-d', n)))) % 1000000
  FROM #Numbers
 WHERE n <= @customerCount;

SET IDENTITY_INSERT dbo.Customers ON;

INSERT dbo.Customers
    (Id, CustomerNumber, Name, ContactName, Email, Phone,
     BillingLine1, BillingLine2, BillingCity, BillingRegion, BillingPostalCode, BillingCountry,
     ShippingLine1, ShippingLine2, ShippingCity, ShippingRegion, ShippingPostalCode, ShippingCountry,
     PaymentTermsDays, CreditLimit, Status, Notes,
     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
SELECT c.n,
       'CUST-' + RIGHT('000000' + CAST(c.n AS VARCHAR(10)), 6),
       f.w + N' ' + s.w,
       fn.w + N' ' + ln.w,
       LOWER(fn.w + N'.' + ln.w + N'@' + REPLACE(REPLACE(f.w + s.w, N' ', N''), N'.', N'') + N'.example.com'),
       '(' + CAST(200 + c.h1 % 800 AS VARCHAR(3)) + ') 555-' + RIGHT('0000' + CAST(c.h2 % 10000 AS VARCHAR(4)), 4),
       CAST(100 + c.h3 % 9800 AS VARCHAR(5)) + N' ' + st.w,
       CASE WHEN c.h4 % 5 = 0 THEN N'Unit ' + CAST(1 + c.h4 % 40 AS VARCHAR(3)) END,
       ci.City, ci.Region, ci.Postal + RIGHT('00' + CAST(c.h2 % 100 AS VARCHAR(2)), 2), N'USA',
       CASE WHEN c.h3 % 10 < 3 THEN CAST(1 + c.h1 % 999 AS VARCHAR(4)) + N' ' + st2.w END,
       NULL,
       CASE WHEN c.h3 % 10 < 3 THEN ci.City END,
       CASE WHEN c.h3 % 10 < 3 THEN ci.Region END,
       CASE WHEN c.h3 % 10 < 3 THEN ci.Postal + RIGHT('00' + CAST(c.h1 % 100 AS VARCHAR(2)), 2) END,
       CASE WHEN c.h3 % 10 < 3 THEN N'USA' END,
       CASE c.h2 % 4 WHEN 0 THEN 14 WHEN 1 THEN 30 WHEN 2 THEN 45 ELSE 60 END,
       CASE WHEN c.h1 % 10 < 6 THEN (1 + c.h4 % 20) * 5000.00 END,
       CASE WHEN c.h4 % 100 < 90 THEN 'Active' WHEN c.h4 % 100 < 95 THEN 'OnHold' ELSE 'Inactive' END,
       NULL,
       DATEADD(HOUR, 9 + c.h1 % 8, CAST(DATEADD(DAY, -(@historyDays + c.h2 % 400), @baseDate) AS DATETIME2(0))),
       @seedUser,
       DATEADD(HOUR, 9 + c.h1 % 8, CAST(DATEADD(DAY, -(@historyDays + c.h2 % 400), @baseDate) AS DATETIME2(0))),
       @seedUser
  FROM #Cust c
  JOIN #CompanyFirst  f   ON f.i   = 1 + (c.n - 1) % 20
  JOIN #CompanySecond s   ON s.i   = 1 + (c.n - 1) / 20
  JOIN #FirstNames    fn  ON fn.i  = 1 + c.h1 % 12
  JOIN #LastNames     ln  ON ln.i  = 1 + c.h2 % 12
  JOIN #Streets       st  ON st.i  = 1 + c.h3 % 10
  JOIN #Streets       st2 ON st2.i = 1 + c.h4 % 10
  JOIN #Cities        ci  ON ci.i  = 1 + c.h1 % 15
 ORDER BY c.n;

SET IDENTITY_INSERT dbo.Customers OFF;

/* ---------------------------------------------------------------------------
   Products
   --------------------------------------------------------------------------- */

CREATE TABLE #Prod
(
    n  INT NOT NULL PRIMARY KEY,
    h1 INT NOT NULL, h2 INT NOT NULL, h3 INT NOT NULL
);

INSERT #Prod (n, h1, h2, h3)
SELECT n,
       ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('prod-a', n)))) % 1000000,
       ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('prod-b', n)))) % 1000000,
       ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('prod-c', n)))) % 1000000
  FROM #Numbers
 WHERE n <= @productCount;

SET IDENTITY_INSERT dbo.Products ON;

INSERT dbo.Products
    (Id, Sku, Name, Description, Category, UnitOfMeasure, UnitPrice, CostPrice, QuantityOnHand, ReorderLevel, IsActive,
     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
SELECT p.n,
       cat.Prefix + '-' + RIGHT('0000' + CAST(p.n AS VARCHAR(10)), 4),
       it.Name + N' - ' + v.w,
       it.Name + N' (' + v.w + N'). Sold per ' + cat.Uom + N'.',
       cat.Name,
       cat.Uom,
       price.UnitPrice,
       ROUND(price.UnitPrice * (0.55 + (p.h2 % 21) / 100.0), 2),
       CASE WHEN p.h3 % 10 = 0 THEN 0 ELSE p.h3 % 500 END,
       10 + p.h1 % 41,
       CASE WHEN p.h2 % 100 < 95 THEN 1 ELSE 0 END,
       DATEADD(DAY, -(@historyDays + 60), CAST(@baseDate AS DATETIME2(0))),
       @seedUser,
       DATEADD(DAY, -(@historyDays + 60), CAST(@baseDate AS DATETIME2(0))),
       @seedUser
  FROM #Prod p
  JOIN #Categories cat ON cat.i = 1 + (p.n - 1) % 8
  JOIN #Items      it  ON it.Cat = cat.i AND it.k = ((p.n - 1) / 8) % 5
  JOIN #Variants   v   ON v.i = ((p.n - 1) / 8) / 5
  CROSS APPLY (SELECT CAST(1.50 + (p.h1 % 44850) / 100.0 AS DECIMAL(18,2)) AS UnitPrice) price
 ORDER BY p.n;

SET IDENTITY_INSERT dbo.Products OFF;

/* ---------------------------------------------------------------------------
   Orders
   --------------------------------------------------------------------------- */

CREATE TABLE #Ord
(
    n          INT NOT NULL PRIMARY KEY,
    h1 INT NOT NULL, h2 INT NOT NULL, h3 INT NOT NULL, h4 INT NOT NULL, h5 INT NOT NULL,
    CustomerId INT NOT NULL,
    DaysAgo    INT NOT NULL,
    OrderDate  DATE NOT NULL,
    Status     VARCHAR(20) NOT NULL,
    CreatedAt  DATETIME2(0) NOT NULL
);

INSERT #Ord (n, h1, h2, h3, h4, h5, CustomerId, DaysAgo, OrderDate, Status, CreatedAt)
SELECT x.n, x.h1, x.h2, x.h3, x.h4, x.h5,
       x.CustomerId,
       x.DaysAgo,
       x.OrderDate,
       CASE
            WHEN x.DaysAgo >= 45 THEN
                 CASE WHEN x.h3 % 100 < 85 THEN 'Invoiced'
                      WHEN x.h3 % 100 < 90 THEN 'Fulfilled'
                      ELSE 'Cancelled' END
            WHEN x.DaysAgo >= 14 THEN
                 CASE WHEN x.h3 % 100 < 30 THEN 'Invoiced'
                      WHEN x.h3 % 100 < 65 THEN 'Fulfilled'
                      WHEN x.h3 % 100 < 85 THEN 'Approved'
                      WHEN x.h3 % 100 < 92 THEN 'Submitted'
                      ELSE 'Cancelled' END
            ELSE
                 CASE WHEN x.h3 % 100 < 25 THEN 'Draft'
                      WHEN x.h3 % 100 < 55 THEN 'Submitted'
                      WHEN x.h3 % 100 < 85 THEN 'Approved'
                      WHEN x.h3 % 100 < 95 THEN 'Fulfilled'
                      ELSE 'Cancelled' END
       END,
       DATEADD(MINUTE, x.h4 % 60, DATEADD(HOUR, 8 + x.h4 % 9, CAST(x.OrderDate AS DATETIME2(0))))
  FROM
      (SELECT n,
              h1, h2, h3, h4, h5,
              /* 40% of orders go to the first 30 customers so the top-customer report has a shape. */
              CASE WHEN h1 % 10 < 4 THEN 1 + h1 % 30 ELSE 1 + h1 % @customerCount END AS CustomerId,
              h2 % @historyDays AS DaysAgo,
              DATEADD(DAY, -(h2 % @historyDays), @baseDate) AS OrderDate
         FROM
             (SELECT n,
                     ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('ord-a', n)))) % 1000000 AS h1,
                     ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('ord-b', n)))) % 1000000 AS h2,
                     ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('ord-c', n)))) % 1000000 AS h3,
                     ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('ord-d', n)))) % 1000000 AS h4,
                     ABS(CHECKSUM(HASHBYTES('MD5', CONCAT('ord-e', n)))) % 1000000 AS h5
                FROM #Numbers
               WHERE n <= @orderCount) r) x;

SET IDENTITY_INSERT dbo.Orders ON;

INSERT dbo.Orders
    (Id, OrderNumber, CustomerId, Status, OrderDate, RequiredDate, CustomerReference,
     ShipToLine1, ShipToLine2, ShipToCity, ShipToRegion, ShipToPostalCode, ShipToCountry,
     Subtotal, TaxRate, TaxAmount, Total, Notes,
     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
SELECT o.n,
       'ORD-' + CAST(YEAR(o.OrderDate) AS VARCHAR(4)) + '-' +
           RIGHT('000000' + CAST(ROW_NUMBER() OVER (PARTITION BY YEAR(o.OrderDate) ORDER BY o.OrderDate, o.n) AS VARCHAR(10)), 6),
       o.CustomerId,
       o.Status,
       o.OrderDate,
       CASE WHEN o.h4 % 10 < 7 THEN DATEADD(DAY, 7 + o.h4 % 21, o.OrderDate) END,
       CASE WHEN o.h5 % 2 = 0 THEN 'PO-' + RIGHT('00000' + CAST(o.h5 % 100000 AS VARCHAR(6)), 5) END,
       ISNULL(c.ShippingLine1, c.BillingLine1),
       CASE WHEN c.ShippingLine1 IS NULL THEN c.BillingLine2 ELSE c.ShippingLine2 END,
       ISNULL(c.ShippingCity, c.BillingCity),
       ISNULL(c.ShippingRegion, c.BillingRegion),
       ISNULL(c.ShippingPostalCode, c.BillingPostalCode),
       ISNULL(c.ShippingCountry, c.BillingCountry),
       0, @taxRate, 0, 0, NULL,
       o.CreatedAt, @seedUser, o.CreatedAt, @seedUser
  FROM #Ord o
  JOIN dbo.Customers c ON c.Id = o.CustomerId
 ORDER BY o.n;

SET IDENTITY_INSERT dbo.Orders OFF;

/* ---------------------------------------------------------------------------
   Order lines: one to six per order, distinct products, price and cost
   snapshots taken from the product, occasional discount.
   --------------------------------------------------------------------------- */

INSERT dbo.OrderLines (OrderId, LineNumber, ProductId, Sku, Description, Quantity, UnitPrice, UnitCost, DiscountPercent, LineTotal)
SELECT l.OrderId,
       ROW_NUMBER() OVER (PARTITION BY l.OrderId ORDER BY l.m),
       l.ProductId,
       p.Sku,
       p.Name,
       l.Quantity,
       p.UnitPrice,
       p.CostPrice,
       l.DiscountPercent,
       ROUND(l.Quantity * p.UnitPrice * (1 - l.DiscountPercent / 100.0), 2)
  FROM
      (SELECT d.OrderId, d.m, d.ProductId, d.Quantity, d.DiscountPercent,
              ROW_NUMBER() OVER (PARTITION BY d.OrderId, d.ProductId ORDER BY d.m) AS dup
         FROM
             (SELECT o.n AS OrderId,
                     m.n AS m,
                     1 + (o.h5 + m.n * 7919) % @productCount AS ProductId,
                     CAST(1 + (o.h1 + m.n * 104729) % 20 AS DECIMAL(18,3)) AS Quantity,
                     CAST(CASE WHEN (o.h2 + m.n) % 10 = 0 THEN 5 + (o.h3 + m.n) % 11 ELSE 0 END AS DECIMAL(5,2)) AS DiscountPercent
                FROM #Ord o
                JOIN #Numbers m ON m.n <= 1 + o.h5 % 6) d) l
  JOIN dbo.Products p ON p.Id = l.ProductId
 WHERE l.dup = 1
 ORDER BY l.OrderId, l.m;

UPDATE o
   SET Subtotal  = t.Subtotal,
       TaxAmount = ROUND(t.Subtotal * @taxRate, 2),
       Total     = t.Subtotal + ROUND(t.Subtotal * @taxRate, 2)
  FROM dbo.Orders o
  JOIN (SELECT OrderId, SUM(LineTotal) AS Subtotal FROM dbo.OrderLines GROUP BY OrderId) t ON t.OrderId = o.Id;

/* ---------------------------------------------------------------------------
   Status history: the full chain each order walked to reach its status.
   --------------------------------------------------------------------------- */

CREATE TABLE #Path (Step INT NOT NULL PRIMARY KEY, Status VARCHAR(20) NOT NULL, OffsetHours INT NOT NULL);
INSERT #Path (Step, Status, OffsetHours) VALUES
    (0, 'Draft', 0), (1, 'Submitted', 2), (2, 'Approved', 26), (3, 'Fulfilled', 98), (4, 'Invoiced', 194);

CREATE TABLE #OrdStep
(
    n         INT NOT NULL PRIMARY KEY,
    FinalStep INT NOT NULL,        /* last step on the normal path that was reached */
    Cancelled BIT NOT NULL
);

INSERT #OrdStep (n, FinalStep, Cancelled)
SELECT o.n,
       CASE o.Status
            WHEN 'Draft'     THEN 0
            WHEN 'Submitted' THEN 1
            WHEN 'Approved'  THEN 2
            WHEN 'Fulfilled' THEN 3
            WHEN 'Invoiced'  THEN 4
            ELSE o.h4 % 3    /* cancelled from Draft, Submitted or Approved */
       END,
       CASE WHEN o.Status = 'Cancelled' THEN 1 ELSE 0 END
  FROM #Ord o;

INSERT dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Comment)
SELECT o.n,
       prev.Status,
       p.Status,
       DATEADD(HOUR, p.OffsetHours, o.CreatedAt),
       @seedUser,
       NULL
  FROM #Ord o
  JOIN #OrdStep s ON s.n = o.n
  JOIN #Path p ON p.Step <= s.FinalStep
  LEFT JOIN #Path prev ON prev.Step = p.Step - 1
UNION ALL
SELECT o.n,
       p.Status,
       'Cancelled',
       DATEADD(HOUR, p.OffsetHours + 5, o.CreatedAt),
       @seedUser,
       CASE o.h5 % 3 WHEN 0 THEN N'Customer cancelled.' WHEN 1 THEN N'Duplicate order.' ELSE N'Superseded by a revised order.' END
  FROM #Ord o
  JOIN #OrdStep s ON s.n = o.n AND s.Cancelled = 1
  JOIN #Path p ON p.Step = s.FinalStep
 ORDER BY 1, 4;

UPDATE o
   SET UpdatedAtUtc = h.LastChange
  FROM dbo.Orders o
  JOIN (SELECT OrderId, MAX(ChangedAtUtc) AS LastChange FROM dbo.OrderStatusHistory GROUP BY OrderId) h ON h.OrderId = o.Id;

/* ---------------------------------------------------------------------------
   Invoices for invoiced orders, then payments.
   --------------------------------------------------------------------------- */

INSERT dbo.Invoices
    (InvoiceNumber, OrderId, CustomerId, IssuedDate, DueDate, Subtotal, TaxAmount, Total, AmountPaid, Status,
     CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
SELECT 'INV-' + CAST(YEAR(x.IssuedDate) AS VARCHAR(4)) + '-' +
           RIGHT('000000' + CAST(ROW_NUMBER() OVER (PARTITION BY YEAR(x.IssuedDate) ORDER BY x.IssuedDate, x.n) AS VARCHAR(10)), 6),
       x.n,
       x.CustomerId,
       x.IssuedDate,
       DATEADD(DAY, x.PaymentTermsDays, x.IssuedDate),
       x.Subtotal, x.TaxAmount, x.Total,
       0, 'Open',
       x.IssuedAt, @seedUser, x.IssuedAt, @seedUser
  FROM
      (SELECT o.n, o.CustomerId, ord.Subtotal, ord.TaxAmount, ord.Total, c.PaymentTermsDays,
              CAST(DATEADD(HOUR, 194, o.CreatedAt) AS DATE) AS IssuedDate,
              DATEADD(HOUR, 194, o.CreatedAt) AS IssuedAt
         FROM #Ord o
         JOIN dbo.Orders ord   ON ord.Id = o.n
         JOIN dbo.Customers c  ON c.Id = o.CustomerId
        WHERE o.Status = 'Invoiced') x
 ORDER BY x.IssuedDate, x.n;

CREATE TABLE #Pay
(
    InvoiceId INT NOT NULL PRIMARY KEY,
    Outcome   VARCHAR(10) NOT NULL,   /* Paid | Partial | Open */
    Amount    DECIMAL(18,2) NOT NULL,
    PaidDate  DATE NOT NULL,
    Method    VARCHAR(20) NOT NULL,
    Reference NVARCHAR(50) NULL
);

INSERT #Pay (InvoiceId, Outcome, Amount, PaidDate, Method, Reference)
SELECT i.Id,
       x.Outcome,
       CASE x.Outcome WHEN 'Paid' THEN i.Total ELSE ROUND(i.Total * (0.25 + (o.h2 % 50) / 100.0), 2) END,
       x.PaidDate,
       CASE o.h3 % 4 WHEN 0 THEN 'BankTransfer' WHEN 1 THEN 'Cheque' WHEN 2 THEN 'Card' ELSE 'Cash' END,
       CASE o.h3 % 4 WHEN 0 THEN N'TXN-' WHEN 1 THEN N'CHK-' WHEN 2 THEN N'CARD-' ELSE NULL END
           + CASE WHEN o.h3 % 4 = 3 THEN N'' ELSE RIGHT('000000' + CAST(o.h1 % 1000000 AS VARCHAR(6)), 6) END
  FROM dbo.Invoices i
  JOIN #Ord o ON o.n = i.OrderId
  CROSS APPLY
      (SELECT CASE
                   /* The older the invoice, the more likely it has been settled. */
                   WHEN DATEDIFF(DAY, i.DueDate, @baseDate) > 90 THEN
                        CASE WHEN o.h4 % 100 < 95 THEN 'Paid' WHEN o.h4 % 100 < 98 THEN 'Partial' ELSE 'Open' END
                   WHEN DATEDIFF(DAY, i.DueDate, @baseDate) > 30 THEN
                        CASE WHEN o.h4 % 100 < 85 THEN 'Paid' WHEN o.h4 % 100 < 92 THEN 'Partial' ELSE 'Open' END
                   WHEN i.DueDate < @baseDate THEN
                        CASE WHEN o.h4 % 100 < 65 THEN 'Paid' WHEN o.h4 % 100 < 78 THEN 'Partial' ELSE 'Open' END
                   ELSE
                        CASE WHEN o.h4 % 100 < 35 THEN 'Paid' WHEN o.h4 % 100 < 45 THEN 'Partial' ELSE 'Open' END
              END AS Outcome,
              CASE WHEN DATEADD(DAY, o.h5 % 45, i.IssuedDate) > @baseDate THEN @baseDate
                   ELSE DATEADD(DAY, o.h5 % 45, i.IssuedDate) END AS PaidDate) x
 WHERE x.Outcome <> 'Open';

INSERT dbo.Payments (InvoiceId, PaidDate, Amount, Method, Reference, Notes, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
SELECT p.InvoiceId, p.PaidDate, p.Amount, p.Method, p.Reference, NULL,
       DATEADD(HOUR, 10, CAST(p.PaidDate AS DATETIME2(0))), @seedUser,
       DATEADD(HOUR, 10, CAST(p.PaidDate AS DATETIME2(0))), @seedUser
  FROM #Pay p
 ORDER BY p.PaidDate, p.InvoiceId;

UPDATE i
   SET AmountPaid   = p.Amount,
       Status       = CASE WHEN p.Amount = i.Total THEN 'Paid' ELSE 'PartiallyPaid' END,
       UpdatedAtUtc = DATEADD(HOUR, 10, CAST(p.PaidDate AS DATETIME2(0)))
  FROM dbo.Invoices i
  JOIN #Pay p ON p.InvoiceId = i.Id;

/* ---------------------------------------------------------------------------
   Notes on some customers and orders.
   --------------------------------------------------------------------------- */

INSERT dbo.Notes (EntityType, EntityId, Body, CreatedAtUtc, CreatedBy)
SELECT 'Customer', c.n, t.w,
       DATEADD(DAY, c.h3 % 300, DATEADD(DAY, -600, CAST(@baseDate AS DATETIME2(0)))), @seedUser
  FROM #Cust c
  JOIN #NoteTexts t ON t.i = c.h4 % 8
 WHERE c.h2 % 3 = 0
UNION ALL
SELECT 'Order', o.n, t.w, DATEADD(HOUR, 1, o.CreatedAt), @seedUser
  FROM #Ord o
  JOIN #NoteTexts t ON t.i = o.h4 % 8
 WHERE o.h1 % 10 = 0;

/* ---------------------------------------------------------------------------
   Bring the number generators in line with what was inserted.
   --------------------------------------------------------------------------- */

INSERT dbo.NumberSeries (SeriesCode, SeriesYear, NextValue)
SELECT 'ORD', YEAR(OrderDate), COUNT(*) + 1
  FROM dbo.Orders
 GROUP BY YEAR(OrderDate);

INSERT dbo.NumberSeries (SeriesCode, SeriesYear, NextValue)
SELECT 'INV', YEAR(IssuedDate), COUNT(*) + 1
  FROM dbo.Invoices
 GROUP BY YEAR(IssuedDate);

DECLARE @restart NVARCHAR(200) = N'ALTER SEQUENCE dbo.seq_CustomerNumber RESTART WITH ' + CAST(@customerCount + 1 AS NVARCHAR(10)) + N';';
EXEC (@restart);

COMMIT TRANSACTION;

DECLARE @summary NVARCHAR(400) =
    N'Seed complete: ' +
    CAST((SELECT COUNT(*) FROM dbo.Customers)  AS NVARCHAR(10)) + N' customers, ' +
    CAST((SELECT COUNT(*) FROM dbo.Products)   AS NVARCHAR(10)) + N' products, ' +
    CAST((SELECT COUNT(*) FROM dbo.Orders)     AS NVARCHAR(10)) + N' orders, ' +
    CAST((SELECT COUNT(*) FROM dbo.OrderLines) AS NVARCHAR(10)) + N' lines, ' +
    CAST((SELECT COUNT(*) FROM dbo.Invoices)   AS NVARCHAR(10)) + N' invoices, ' +
    CAST((SELECT COUNT(*) FROM dbo.Payments)   AS NVARCHAR(10)) + N' payments.';
PRINT @summary;
