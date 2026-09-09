/* ---------------------------------------------------------------------------
   0003  Products
   Purpose : Product master with pricing, cost, unit of measure and a
             simple on-hand quantity with reorder level.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Products', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Products
    (
        Id              INT            IDENTITY(1,1) NOT NULL,
        Sku             VARCHAR(40)    NOT NULL,
        Name            NVARCHAR(200)  NOT NULL,
        Description     NVARCHAR(1000) NULL,
        Category        NVARCHAR(60)   NOT NULL,
        UnitOfMeasure   VARCHAR(10)    NOT NULL CONSTRAINT DF_Products_UnitOfMeasure DEFAULT ('EA'),
        UnitPrice       DECIMAL(18,2)  NOT NULL,
        CostPrice       DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Products_CostPrice DEFAULT (0),
        QuantityOnHand  DECIMAL(18,3)  NOT NULL CONSTRAINT DF_Products_QuantityOnHand DEFAULT (0),
        ReorderLevel    DECIMAL(18,3)  NOT NULL CONSTRAINT DF_Products_ReorderLevel DEFAULT (0),
        IsActive        BIT            NOT NULL CONSTRAINT DF_Products_IsActive DEFAULT (1),

        CreatedAtUtc    DATETIME2(0)   NOT NULL CONSTRAINT DF_Products_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedBy       NVARCHAR(100)  NOT NULL,
        UpdatedAtUtc    DATETIME2(0)   NOT NULL CONSTRAINT DF_Products_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy       NVARCHAR(100)  NOT NULL,
        RowVersion      ROWVERSION     NOT NULL,

        CONSTRAINT PK_Products PRIMARY KEY CLUSTERED (Id),
        /* SKUs are stored upper-case so the unique index is meaningful regardless of collation. */
        CONSTRAINT CK_Products_Sku_Upper CHECK (Sku = UPPER(Sku) COLLATE Latin1_General_CS_AS AND LEN(Sku) > 0),
        CONSTRAINT CK_Products_UnitPrice CHECK (UnitPrice >= 0),
        CONSTRAINT CK_Products_CostPrice CHECK (CostPrice >= 0),
        CONSTRAINT CK_Products_ReorderLevel CHECK (ReorderLevel >= 0)
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Products_Sku ON dbo.Products (Sku);
    CREATE NONCLUSTERED INDEX IX_Products_Name ON dbo.Products (Name) INCLUDE (Sku, IsActive, UnitPrice);
    CREATE NONCLUSTERED INDEX IX_Products_Category ON dbo.Products (Category, Name);
    /* Supports the low-stock report without scanning inactive products. */
    CREATE NONCLUSTERED INDEX IX_Products_Active_Stock ON dbo.Products (QuantityOnHand, ReorderLevel)
        INCLUDE (Sku, Name) WHERE IsActive = 1;
END
GO
