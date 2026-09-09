/* ---------------------------------------------------------------------------
   0004  Orders, order lines and status history
   Purpose : Sales order header with stored totals, line items with price
             and cost snapshots, and an append-only status change log.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Orders', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Orders
    (
        Id                INT            IDENTITY(1,1) NOT NULL,
        OrderNumber       VARCHAR(16)    NOT NULL,
        CustomerId        INT            NOT NULL,
        Status            VARCHAR(20)    NOT NULL CONSTRAINT DF_Orders_Status DEFAULT ('Draft'),
        OrderDate         DATE           NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT (CAST(SYSUTCDATETIME() AS DATE)),
        RequiredDate      DATE           NULL,
        CustomerReference NVARCHAR(50)   NULL,

        /* Ship-to is copied from the customer at order time so later address
           changes do not rewrite history. */
        ShipToLine1       NVARCHAR(100)  NULL,
        ShipToLine2       NVARCHAR(100)  NULL,
        ShipToCity        NVARCHAR(80)   NULL,
        ShipToRegion      NVARCHAR(80)   NULL,
        ShipToPostalCode  NVARCHAR(20)   NULL,
        ShipToCountry     NVARCHAR(60)   NULL,

        /* Totals are stored, not computed, so that list screens and reports
           never have to aggregate lines. The application recalculates them
           on every line change. */
        Subtotal          DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Orders_Subtotal DEFAULT (0),
        TaxRate           DECIMAL(9,6)   NOT NULL CONSTRAINT DF_Orders_TaxRate DEFAULT (0),
        TaxAmount         DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Orders_TaxAmount DEFAULT (0),
        Total             DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Orders_Total DEFAULT (0),
        Notes             NVARCHAR(MAX)  NULL,

        CreatedAtUtc      DATETIME2(0)   NOT NULL CONSTRAINT DF_Orders_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedBy         NVARCHAR(100)  NOT NULL,
        UpdatedAtUtc      DATETIME2(0)   NOT NULL CONSTRAINT DF_Orders_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy         NVARCHAR(100)  NOT NULL,
        RowVersion        ROWVERSION     NOT NULL,

        CONSTRAINT PK_Orders PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Orders_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
        CONSTRAINT CK_Orders_Status CHECK (Status IN ('Draft', 'Submitted', 'Approved', 'Fulfilled', 'Invoiced', 'Cancelled')),
        CONSTRAINT CK_Orders_RequiredDate CHECK (RequiredDate IS NULL OR RequiredDate >= OrderDate),
        CONSTRAINT CK_Orders_TaxRate CHECK (TaxRate >= 0 AND TaxRate < 1),
        CONSTRAINT CK_Orders_Totals CHECK (Subtotal >= 0 AND TaxAmount >= 0 AND Total = Subtotal + TaxAmount)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Orders_OrderNumber ON dbo.Orders (OrderNumber);
    /* Customer detail page: that customer's orders newest first. */
    CREATE NONCLUSTERED INDEX IX_Orders_CustomerId ON dbo.Orders (CustomerId, OrderDate DESC) INCLUDE (OrderNumber, Status, Total);
    /* Order list filtered by status, and the dashboard counts. */
    CREATE NONCLUSTERED INDEX IX_Orders_Status_OrderDate ON dbo.Orders (Status, OrderDate DESC) INCLUDE (OrderNumber, CustomerId, Total);
    /* Date-range reports. */
    CREATE NONCLUSTERED INDEX IX_Orders_OrderDate ON dbo.Orders (OrderDate) INCLUDE (Status, CustomerId, Subtotal, TaxAmount, Total);
END
GO

IF OBJECT_ID(N'dbo.OrderLines', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderLines
    (
        Id              INT            IDENTITY(1,1) NOT NULL,
        OrderId         INT            NOT NULL,
        LineNumber      INT            NOT NULL,
        ProductId       INT            NOT NULL,

        /* Snapshots: what the product was called and cost when the line was
           added. Product changes must not alter historical orders. */
        Sku             VARCHAR(40)    NOT NULL,
        Description     NVARCHAR(200)  NOT NULL,
        Quantity        DECIMAL(18,3)  NOT NULL,
        UnitPrice       DECIMAL(18,2)  NOT NULL,
        UnitCost        DECIMAL(18,2)  NOT NULL CONSTRAINT DF_OrderLines_UnitCost DEFAULT (0),
        DiscountPercent DECIMAL(5,2)   NOT NULL CONSTRAINT DF_OrderLines_DiscountPercent DEFAULT (0),
        LineTotal       DECIMAL(18,2)  NOT NULL,

        CONSTRAINT PK_OrderLines PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_OrderLines_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id) ON DELETE CASCADE,
        CONSTRAINT FK_OrderLines_Products FOREIGN KEY (ProductId) REFERENCES dbo.Products (Id),
        CONSTRAINT CK_OrderLines_Quantity CHECK (Quantity > 0),
        CONSTRAINT CK_OrderLines_UnitPrice CHECK (UnitPrice >= 0),
        CONSTRAINT CK_OrderLines_UnitCost CHECK (UnitCost >= 0),
        CONSTRAINT CK_OrderLines_DiscountPercent CHECK (DiscountPercent BETWEEN 0 AND 100),
        CONSTRAINT CK_OrderLines_LineTotal CHECK (LineTotal >= 0)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_OrderLines_OrderId_LineNumber ON dbo.OrderLines (OrderId, LineNumber);
    /* Product sales report and "which orders contain this product". */
    CREATE NONCLUSTERED INDEX IX_OrderLines_ProductId ON dbo.OrderLines (ProductId) INCLUDE (OrderId, Quantity, LineTotal, UnitCost);
END
GO

/* Append-only. Rows are never updated or deleted, so it carries only the
   "who/when" of the change rather than the full audit column set. */
IF OBJECT_ID(N'dbo.OrderStatusHistory', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.OrderStatusHistory
    (
        Id           INT            IDENTITY(1,1) NOT NULL,
        OrderId      INT            NOT NULL,
        FromStatus   VARCHAR(20)    NULL,
        ToStatus     VARCHAR(20)    NOT NULL,
        ChangedAtUtc DATETIME2(0)   NOT NULL CONSTRAINT DF_OrderStatusHistory_ChangedAtUtc DEFAULT (SYSUTCDATETIME()),
        ChangedBy    NVARCHAR(100)  NOT NULL,
        Comment      NVARCHAR(500)  NULL,

        CONSTRAINT PK_OrderStatusHistory PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_OrderStatusHistory_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id) ON DELETE CASCADE,
        CONSTRAINT CK_OrderStatusHistory_FromStatus CHECK (FromStatus IS NULL OR FromStatus IN ('Draft', 'Submitted', 'Approved', 'Fulfilled', 'Invoiced', 'Cancelled')),
        CONSTRAINT CK_OrderStatusHistory_ToStatus CHECK (ToStatus IN ('Draft', 'Submitted', 'Approved', 'Fulfilled', 'Invoiced', 'Cancelled'))
    );

    CREATE NONCLUSTERED INDEX IX_OrderStatusHistory_OrderId ON dbo.OrderStatusHistory (OrderId, ChangedAtUtc);
    /* Audit page: all changes across orders, newest first. */
    CREATE NONCLUSTERED INDEX IX_OrderStatusHistory_ChangedAtUtc ON dbo.OrderStatusHistory (ChangedAtUtc DESC) INCLUDE (OrderId, ToStatus, ChangedBy);
END
GO
