/* ---------------------------------------------------------------------------
   0002  Customers
   Purpose : Customer master table with billing and optional shipping
             address, payment terms, credit limit and status.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Customers', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Customers
    (
        Id                 INT            IDENTITY(1,1) NOT NULL,
        CustomerNumber     VARCHAR(12)    NOT NULL,
        Name               NVARCHAR(200)  NOT NULL,
        ContactName        NVARCHAR(100)  NULL,
        Email              NVARCHAR(254)  NULL,
        Phone              NVARCHAR(30)   NULL,

        BillingLine1       NVARCHAR(100)  NOT NULL,
        BillingLine2       NVARCHAR(100)  NULL,
        BillingCity        NVARCHAR(80)   NOT NULL,
        BillingRegion      NVARCHAR(80)   NULL,
        BillingPostalCode  NVARCHAR(20)   NULL,
        BillingCountry     NVARCHAR(60)   NOT NULL,

        /* Shipping address is optional; NULL means "same as billing". */
        ShippingLine1      NVARCHAR(100)  NULL,
        ShippingLine2      NVARCHAR(100)  NULL,
        ShippingCity       NVARCHAR(80)   NULL,
        ShippingRegion     NVARCHAR(80)   NULL,
        ShippingPostalCode NVARCHAR(20)   NULL,
        ShippingCountry    NVARCHAR(60)   NULL,

        PaymentTermsDays   INT            NOT NULL CONSTRAINT DF_Customers_PaymentTermsDays DEFAULT (30),
        CreditLimit        DECIMAL(18,2)  NULL,
        Status             VARCHAR(20)    NOT NULL CONSTRAINT DF_Customers_Status DEFAULT ('Active'),
        Notes              NVARCHAR(MAX)  NULL,

        CreatedAtUtc       DATETIME2(0)   NOT NULL CONSTRAINT DF_Customers_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedBy          NVARCHAR(100)  NOT NULL,
        UpdatedAtUtc       DATETIME2(0)   NOT NULL CONSTRAINT DF_Customers_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy          NVARCHAR(100)  NOT NULL,
        RowVersion         ROWVERSION     NOT NULL,

        CONSTRAINT PK_Customers PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT CK_Customers_Status CHECK (Status IN ('Active', 'OnHold', 'Inactive')),
        CONSTRAINT CK_Customers_PaymentTermsDays CHECK (PaymentTermsDays BETWEEN 0 AND 365),
        CONSTRAINT CK_Customers_CreditLimit CHECK (CreditLimit IS NULL OR CreditLimit >= 0),
        CONSTRAINT CK_Customers_ShippingAddress CHECK
        (
            /* Either no shipping address at all, or at least line 1, city and country. */
            (ShippingLine1 IS NULL AND ShippingCity IS NULL AND ShippingCountry IS NULL)
            OR
            (ShippingLine1 IS NOT NULL AND ShippingCity IS NOT NULL AND ShippingCountry IS NOT NULL)
        )
    );

    /* Lookups by number are exact; by name and email are prefix searches. */
    CREATE UNIQUE NONCLUSTERED INDEX UX_Customers_CustomerNumber ON dbo.Customers (CustomerNumber);
    CREATE NONCLUSTERED INDEX IX_Customers_Name ON dbo.Customers (Name) INCLUDE (CustomerNumber, Status);
    CREATE NONCLUSTERED INDEX IX_Customers_Email ON dbo.Customers (Email) WHERE Email IS NOT NULL;
    CREATE NONCLUSTERED INDEX IX_Customers_Status ON dbo.Customers (Status, Name);
END
GO
