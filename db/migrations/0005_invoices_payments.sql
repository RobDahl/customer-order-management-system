/* ---------------------------------------------------------------------------
   0005  Invoices and payments
   Purpose : One invoice per fulfilled order, with running AmountPaid
             maintained by the payment procedure, and the payments that
             settle it.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

IF OBJECT_ID(N'dbo.Invoices', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Invoices
    (
        Id             INT            IDENTITY(1,1) NOT NULL,
        InvoiceNumber  VARCHAR(16)    NOT NULL,
        OrderId        INT            NOT NULL,
        CustomerId     INT            NOT NULL,
        IssuedDate     DATE           NOT NULL,
        DueDate        DATE           NOT NULL,
        Subtotal       DECIMAL(18,2)  NOT NULL,
        TaxAmount      DECIMAL(18,2)  NOT NULL,
        Total          DECIMAL(18,2)  NOT NULL,
        AmountPaid     DECIMAL(18,2)  NOT NULL CONSTRAINT DF_Invoices_AmountPaid DEFAULT (0),
        Status         VARCHAR(20)    NOT NULL CONSTRAINT DF_Invoices_Status DEFAULT ('Open'),

        CreatedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_Invoices_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedBy      NVARCHAR(100)  NOT NULL,
        UpdatedAtUtc   DATETIME2(0)   NOT NULL CONSTRAINT DF_Invoices_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy      NVARCHAR(100)  NOT NULL,
        RowVersion     ROWVERSION     NOT NULL,

        CONSTRAINT PK_Invoices PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Invoices_Orders FOREIGN KEY (OrderId) REFERENCES dbo.Orders (Id),
        CONSTRAINT FK_Invoices_Customers FOREIGN KEY (CustomerId) REFERENCES dbo.Customers (Id),
        CONSTRAINT CK_Invoices_Status CHECK (Status IN ('Open', 'PartiallyPaid', 'Paid', 'Void')),
        CONSTRAINT CK_Invoices_DueDate CHECK (DueDate >= IssuedDate),
        CONSTRAINT CK_Invoices_Totals CHECK (Subtotal >= 0 AND TaxAmount >= 0 AND Total = Subtotal + TaxAmount),
        CONSTRAINT CK_Invoices_AmountPaid CHECK (AmountPaid >= 0 AND AmountPaid <= Total),
        /* Status is derived from AmountPaid; the constraint keeps the two honest. */
        CONSTRAINT CK_Invoices_Status_AmountPaid CHECK
        (
            (Status = 'Void'          AND AmountPaid = 0)
         OR (Status = 'Open'          AND AmountPaid = 0)
         OR (Status = 'PartiallyPaid' AND AmountPaid > 0 AND AmountPaid < Total)
         OR (Status = 'Paid'          AND AmountPaid = Total)
        )
    );

    CREATE UNIQUE NONCLUSTERED INDEX UX_Invoices_InvoiceNumber ON dbo.Invoices (InvoiceNumber);
    /* One live invoice per order. A voided invoice stays for the record and
       the order can be invoiced again, so the uniqueness excludes 'Void'. */
    CREATE UNIQUE NONCLUSTERED INDEX UX_Invoices_OrderId_Live ON dbo.Invoices (OrderId) WHERE Status <> 'Void';
    CREATE NONCLUSTERED INDEX IX_Invoices_CustomerId ON dbo.Invoices (CustomerId, IssuedDate DESC) INCLUDE (InvoiceNumber, Status, Total, AmountPaid, DueDate);
    /* Overdue and aging queries: open invoices by due date. */
    CREATE NONCLUSTERED INDEX IX_Invoices_Status_DueDate ON dbo.Invoices (Status, DueDate) INCLUDE (CustomerId, Total, AmountPaid);
END
GO

IF OBJECT_ID(N'dbo.Payments', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.Payments
    (
        Id           INT            IDENTITY(1,1) NOT NULL,
        InvoiceId    INT            NOT NULL,
        PaidDate     DATE           NOT NULL,
        Amount       DECIMAL(18,2)  NOT NULL,
        Method       VARCHAR(20)    NOT NULL,
        Reference    NVARCHAR(50)   NULL,
        Notes        NVARCHAR(500)  NULL,

        CreatedAtUtc DATETIME2(0)   NOT NULL CONSTRAINT DF_Payments_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedBy    NVARCHAR(100)  NOT NULL,
        UpdatedAtUtc DATETIME2(0)   NOT NULL CONSTRAINT DF_Payments_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),
        UpdatedBy    NVARCHAR(100)  NOT NULL,
        RowVersion   ROWVERSION     NOT NULL,

        CONSTRAINT PK_Payments PRIMARY KEY CLUSTERED (Id),
        CONSTRAINT FK_Payments_Invoices FOREIGN KEY (InvoiceId) REFERENCES dbo.Invoices (Id),
        CONSTRAINT CK_Payments_Amount CHECK (Amount > 0),
        CONSTRAINT CK_Payments_Method CHECK (Method IN ('Cash', 'Cheque', 'BankTransfer', 'Card'))
    );

    CREATE NONCLUSTERED INDEX IX_Payments_InvoiceId ON dbo.Payments (InvoiceId, PaidDate);
    CREATE NONCLUSTERED INDEX IX_Payments_PaidDate ON dbo.Payments (PaidDate) INCLUDE (InvoiceId, Amount, Method);
END
GO
