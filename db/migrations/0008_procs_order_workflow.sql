/* ---------------------------------------------------------------------------
   0008  Order workflow procedures
   Purpose : The transitions that must be atomic across several tables:
             fulfilment (stock movement), invoicing, payment and void.
             Simpler transitions (submit, approve, cancel) are single-row
             updates performed by the application layer.

   Error numbers raised by these procedures (mapped to codes in C#):
     50001  Order not found
     50002  Order not in the required status
     50003  Insufficient stock
     50004  Invoice not found
     50005  Live invoice already exists for this order
     50006  Invoice not open
     50007  Payment exceeds outstanding balance
     50008  Invoice has payments and cannot be voided
     50009  Order has no lines
     50010  Row was modified by another user (RowVersion mismatch)

   Every procedure accepts an optional @RowVersion. When supplied it must
   match the current row or the call fails with 50010, so both clients get
   the same optimistic concurrency behaviour as plain updates.

   Pattern: XACT_ABORT ON plus TRY/CATCH that rolls back and rethrows. The
   rollback inside the procedure means a T-SQL caller that catches the
   error is left with a clean session rather than a doomed transaction.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

CREATE OR ALTER PROCEDURE dbo.usp_Order_Fulfil
    @OrderId            INT,
    @ChangedBy          NVARCHAR(100),
    @Comment            NVARCHAR(500) = NULL,
    @AllowNegativeStock BIT           = 0,
    @RowVersion         BINARY(8)     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @status  VARCHAR(20),
            @current BINARY(8),
            @now     DATETIME2(0) = SYSUTCDATETIME(),
            @message NVARCHAR(1000);

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @status = Status, @current = RowVersion
          FROM dbo.Orders WITH (UPDLOCK, HOLDLOCK)
         WHERE Id = @OrderId;

        IF @status IS NULL
            THROW 50001, 'Order not found.', 1;

        IF @RowVersion IS NOT NULL AND @RowVersion <> @current
            THROW 50010, 'The order was modified by another user. Reload and try again.', 1;

        IF @status <> 'Approved'
        BEGIN
            SET @message = 'Order cannot be fulfilled from status ''' + @status + '''.';
            THROW 50002, @message, 1;
        END

        IF NOT EXISTS (SELECT 1 FROM dbo.OrderLines WHERE OrderId = @OrderId)
            THROW 50009, 'Order has no lines.', 1;

        /* Demand per product; a product may appear on more than one line. */
        DECLARE @demand TABLE
        (
            ProductId INT           NOT NULL PRIMARY KEY,
            Quantity  DECIMAL(18,3) NOT NULL
        );

        INSERT @demand (ProductId, Quantity)
        SELECT ProductId, SUM(Quantity)
          FROM dbo.OrderLines
         WHERE OrderId = @OrderId
         GROUP BY ProductId;

        IF @AllowNegativeStock = 0
        BEGIN
            SELECT @message = STRING_AGG(
                       p.Sku + ' (need ' + FORMAT(d.Quantity, '0.###') + ', have ' + FORMAT(p.QuantityOnHand, '0.###') + ')',
                       ', ')
              FROM @demand d
              JOIN dbo.Products p WITH (UPDLOCK) ON p.Id = d.ProductId
             WHERE p.QuantityOnHand < d.Quantity;

            IF @message IS NOT NULL
            BEGIN
                SET @message = 'Insufficient stock: ' + @message;
                THROW 50003, @message, 1;
            END
        END

        UPDATE p
           SET QuantityOnHand = p.QuantityOnHand - d.Quantity,
               UpdatedAtUtc   = @now,
               UpdatedBy      = @ChangedBy
          FROM dbo.Products p
          JOIN @demand d ON d.ProductId = p.Id;

        UPDATE dbo.Orders
           SET Status       = 'Fulfilled',
               UpdatedAtUtc = @now,
               UpdatedBy    = @ChangedBy
         WHERE Id = @OrderId;

        INSERT dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Comment)
        VALUES (@OrderId, @status, 'Fulfilled', @now, @ChangedBy, @Comment);

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Invoice_CreateFromOrder
    @OrderId    INT,
    @CreatedBy  NVARCHAR(100),
    @IssuedDate DATE      = NULL,
    @RowVersion BINARY(8) = NULL,
    @InvoiceId  INT       OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @status        VARCHAR(20),
            @current       BINARY(8),
            @customerId    INT,
            @termsDays     INT,
            @subtotal      DECIMAL(18,2),
            @taxAmount     DECIMAL(18,2),
            @total         DECIMAL(18,2),
            @invoiceNumber VARCHAR(16),
            @now           DATETIME2(0) = SYSUTCDATETIME(),
            @message       NVARCHAR(1000);

    SET @IssuedDate = ISNULL(@IssuedDate, CAST(@now AS DATE));

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @status     = o.Status,
               @current    = o.RowVersion,
               @customerId = o.CustomerId,
               @subtotal   = o.Subtotal,
               @taxAmount  = o.TaxAmount,
               @total      = o.Total,
               @termsDays  = c.PaymentTermsDays
          FROM dbo.Orders o WITH (UPDLOCK, HOLDLOCK)
          JOIN dbo.Customers c ON c.Id = o.CustomerId
         WHERE o.Id = @OrderId;

        IF @status IS NULL
            THROW 50001, 'Order not found.', 1;

        IF @RowVersion IS NOT NULL AND @RowVersion <> @current
            THROW 50010, 'The order was modified by another user. Reload and try again.', 1;

        IF @status <> 'Fulfilled'
        BEGIN
            SET @message = 'Order cannot be invoiced from status ''' + @status + '''.';
            THROW 50002, @message, 1;
        END

        IF EXISTS (SELECT 1 FROM dbo.Invoices WHERE OrderId = @OrderId AND Status <> 'Void')
            THROW 50005, 'A live invoice already exists for this order.', 1;

        EXEC dbo.usp_NextInvoiceNumber @SeriesYear = NULL, @InvoiceNumber = @invoiceNumber OUTPUT;

        INSERT dbo.Invoices
            (InvoiceNumber, OrderId, CustomerId, IssuedDate, DueDate, Subtotal, TaxAmount, Total, AmountPaid, Status,
             CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
        VALUES
            (@invoiceNumber, @OrderId, @customerId, @IssuedDate, DATEADD(DAY, @termsDays, @IssuedDate),
             @subtotal, @taxAmount, @total, 0, 'Open',
             @now, @CreatedBy, @now, @CreatedBy);

        SET @InvoiceId = SCOPE_IDENTITY();

        UPDATE dbo.Orders
           SET Status       = 'Invoiced',
               UpdatedAtUtc = @now,
               UpdatedBy    = @CreatedBy
         WHERE Id = @OrderId;

        INSERT dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Comment)
        VALUES (@OrderId, @status, 'Invoiced', @now, @CreatedBy, 'Invoice ' + @invoiceNumber + ' issued.');

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Invoice_ApplyPayment
    @InvoiceId  INT,
    @Amount     DECIMAL(18,2),
    @PaidDate   DATE,
    @Method     VARCHAR(20),
    @CreatedBy  NVARCHAR(100),
    @Reference  NVARCHAR(50)  = NULL,
    @Notes      NVARCHAR(500) = NULL,
    @RowVersion BINARY(8)     = NULL,
    @PaymentId  INT           OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @status  VARCHAR(20),
            @current BINARY(8),
            @total   DECIMAL(18,2),
            @paid    DECIMAL(18,2),
            @now     DATETIME2(0) = SYSUTCDATETIME(),
            @message NVARCHAR(1000);

    IF @Amount IS NULL OR @Amount <= 0
        THROW 50007, 'Payment amount must be greater than zero.', 1;

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @status = Status, @current = RowVersion, @total = Total, @paid = AmountPaid
          FROM dbo.Invoices WITH (UPDLOCK, HOLDLOCK)
         WHERE Id = @InvoiceId;

        IF @status IS NULL
            THROW 50004, 'Invoice not found.', 1;

        IF @RowVersion IS NOT NULL AND @RowVersion <> @current
            THROW 50010, 'The invoice was modified by another user. Reload and try again.', 1;

        IF @status NOT IN ('Open', 'PartiallyPaid')
        BEGIN
            SET @message = 'Invoice in status ''' + @status + ''' cannot accept payments.';
            THROW 50006, @message, 1;
        END

        IF @paid + @Amount > @total
        BEGIN
            SET @message = 'Payment of ' + FORMAT(@Amount, '0.00') + ' exceeds the outstanding balance of ' + FORMAT(@total - @paid, '0.00') + '.';
            THROW 50007, @message, 1;
        END

        INSERT dbo.Payments (InvoiceId, PaidDate, Amount, Method, Reference, Notes, CreatedAtUtc, CreatedBy, UpdatedAtUtc, UpdatedBy)
        VALUES (@InvoiceId, @PaidDate, @Amount, @Method, @Reference, @Notes, @now, @CreatedBy, @now, @CreatedBy);

        SET @PaymentId = SCOPE_IDENTITY();
        SET @paid = @paid + @Amount;

        UPDATE dbo.Invoices
           SET AmountPaid   = @paid,
               Status       = CASE WHEN @paid = @total THEN 'Paid' ELSE 'PartiallyPaid' END,
               UpdatedAtUtc = @now,
               UpdatedBy    = @CreatedBy
         WHERE Id = @InvoiceId;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_Invoice_Void
    @InvoiceId  INT,
    @ChangedBy  NVARCHAR(100),
    @Comment    NVARCHAR(500) = NULL,
    @RowVersion BINARY(8)     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @status        VARCHAR(20),
            @current       BINARY(8),
            @orderId       INT,
            @orderStatus   VARCHAR(20),
            @invoiceNumber VARCHAR(16),
            @now           DATETIME2(0) = SYSUTCDATETIME();

    BEGIN TRY
        BEGIN TRANSACTION;

        SELECT @status = Status, @current = RowVersion, @orderId = OrderId, @invoiceNumber = InvoiceNumber
          FROM dbo.Invoices WITH (UPDLOCK, HOLDLOCK)
         WHERE Id = @InvoiceId;

        IF @status IS NULL
            THROW 50004, 'Invoice not found.', 1;

        IF @RowVersion IS NOT NULL AND @RowVersion <> @current
            THROW 50010, 'The invoice was modified by another user. Reload and try again.', 1;

        IF @status = 'Void'
            THROW 50006, 'Invoice is already void.', 1;

        IF EXISTS (SELECT 1 FROM dbo.Payments WHERE InvoiceId = @InvoiceId)
            THROW 50008, 'Invoice has payments recorded and cannot be voided.', 1;

        UPDATE dbo.Invoices
           SET Status       = 'Void',
               UpdatedAtUtc = @now,
               UpdatedBy    = @ChangedBy
         WHERE Id = @InvoiceId;

        /* The order returns to Fulfilled so a corrected invoice can be issued.
           The voided invoice stays on file; UX_Invoices_OrderId_Live ignores it. */
        SELECT @orderStatus = Status FROM dbo.Orders WITH (UPDLOCK) WHERE Id = @orderId;

        UPDATE dbo.Orders
           SET Status       = 'Fulfilled',
               UpdatedAtUtc = @now,
               UpdatedBy    = @ChangedBy
         WHERE Id = @orderId;

        INSERT dbo.OrderStatusHistory (OrderId, FromStatus, ToStatus, ChangedAtUtc, ChangedBy, Comment)
        VALUES (@orderId, @orderStatus, 'Fulfilled', @now, @ChangedBy,
                'Invoice ' + @invoiceNumber + ' voided.' + ISNULL(' ' + @Comment, ''));

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH
END
GO
