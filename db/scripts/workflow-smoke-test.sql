/* ---------------------------------------------------------------------------
   Workflow smoke test
   Purpose : Walk one order through fulfilment, invoicing, payment and void
             using the stored procedures, checking each expected failure
             along the way. The procedures run with XACT_ABORT ON, so an
             outer transaction cannot survive the expected failures; the
             script instead restores the rows it touched at the end.
   Usage   : sqlcmd -S (localdb)\MSSQLLocalDB -d Coms -i db/scripts/workflow-smoke-test.sql
   Expects : a seeded database (needs an Approved order with stock).
   --------------------------------------------------------------------------- */

SET NOCOUNT ON;
SET QUOTED_IDENTIFIER ON;   /* required by the filtered indexes; sqlcmd defaults to OFF */

DECLARE @orderId INT, @invoiceId INT, @paymentId INT, @failures INT = 0, @rv BINARY(8), @number VARCHAR(16);

/* Pick an approved order whose lines can all be satisfied from stock. */
SELECT TOP (1) @orderId = o.Id
  FROM dbo.Orders o
 WHERE o.Status = 'Approved'
   AND NOT EXISTS
       (SELECT 1
          FROM dbo.OrderLines l
          JOIN dbo.Products p ON p.Id = l.ProductId
         WHERE l.OrderId = o.Id
         GROUP BY l.ProductId, p.QuantityOnHand
        HAVING SUM(l.Quantity) > p.QuantityOnHand)
 ORDER BY o.Id;

IF @orderId IS NULL
BEGIN
    PRINT 'No suitable approved order found.';
    RETURN;
END

PRINT 'Using order ' + CAST(@orderId AS VARCHAR(10));

DECLARE @stockBefore TABLE (ProductId INT PRIMARY KEY, Qty DECIMAL(18,3));
INSERT @stockBefore
SELECT p.Id, p.QuantityOnHand
  FROM dbo.Products p
 WHERE p.Id IN (SELECT ProductId FROM dbo.OrderLines WHERE OrderId = @orderId);

DECLARE @orderUpdatedAt DATETIME2(0), @orderUpdatedBy NVARCHAR(100);
SELECT @orderUpdatedAt = UpdatedAtUtc, @orderUpdatedBy = UpdatedBy FROM dbo.Orders WHERE Id = @orderId;

/* 1. Invoicing an approved (not fulfilled) order must fail with 50002. */
BEGIN TRY
    EXEC dbo.usp_Invoice_CreateFromOrder @OrderId = @orderId, @CreatedBy = N'smoke', @InvoiceId = @invoiceId OUTPUT;
    PRINT 'FAIL: invoice created from an approved order';
    SET @failures += 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50002 PRINT 'ok  : invoice before fulfilment rejected (50002)';
    ELSE BEGIN PRINT 'FAIL: unexpected error ' + CAST(ERROR_NUMBER() AS VARCHAR(10)) + ' ' + ERROR_MESSAGE(); SET @failures += 1; END
END CATCH

/* 2. Fulfil with a stale RowVersion must fail with 50010. */
BEGIN TRY
    EXEC dbo.usp_Order_Fulfil @OrderId = @orderId, @ChangedBy = N'smoke', @RowVersion = 0x0000000000000001;
    PRINT 'FAIL: stale RowVersion accepted';
    SET @failures += 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50010 PRINT 'ok  : stale RowVersion rejected (50010)';
    ELSE BEGIN PRINT 'FAIL: unexpected error ' + CAST(ERROR_NUMBER() AS VARCHAR(10)) + ' ' + ERROR_MESSAGE(); SET @failures += 1; END
END CATCH

/* 3. Fulfil for real; stock must go down by the ordered quantities. */
SELECT @rv = RowVersion FROM dbo.Orders WHERE Id = @orderId;
EXEC dbo.usp_Order_Fulfil @OrderId = @orderId, @ChangedBy = N'smoke', @Comment = N'smoke test', @RowVersion = @rv;

IF EXISTS
   (SELECT 1
      FROM @stockBefore b
      JOIN dbo.Products p ON p.Id = b.ProductId
      JOIN (SELECT ProductId, SUM(Quantity) AS Qty FROM dbo.OrderLines WHERE OrderId = @orderId GROUP BY ProductId) l ON l.ProductId = b.ProductId
     WHERE p.QuantityOnHand <> b.Qty - l.Qty)
BEGIN PRINT 'FAIL: stock not decremented correctly'; SET @failures += 1; END
ELSE PRINT 'ok  : fulfilled, stock decremented';

IF (SELECT Status FROM dbo.Orders WHERE Id = @orderId) <> 'Fulfilled'
BEGIN PRINT 'FAIL: order status not Fulfilled'; SET @failures += 1; END

/* 4. Fulfilling again must fail with 50002. */
BEGIN TRY
    EXEC dbo.usp_Order_Fulfil @OrderId = @orderId, @ChangedBy = N'smoke';
    PRINT 'FAIL: double fulfilment accepted';
    SET @failures += 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50002 PRINT 'ok  : double fulfilment rejected (50002)';
    ELSE BEGIN PRINT 'FAIL: unexpected error ' + CAST(ERROR_NUMBER() AS VARCHAR(10)); SET @failures += 1; END
END CATCH

/* 5. Invoice it. */
EXEC dbo.usp_Invoice_CreateFromOrder @OrderId = @orderId, @CreatedBy = N'smoke', @IssuedDate = '2026-09-01', @InvoiceId = @invoiceId OUTPUT;

SELECT @number = InvoiceNumber FROM dbo.Invoices WHERE Id = @invoiceId;
IF (SELECT Status FROM dbo.Orders WHERE Id = @orderId) = 'Invoiced'
   AND EXISTS (SELECT 1 FROM dbo.Invoices WHERE Id = @invoiceId AND Status = 'Open' AND AmountPaid = 0)
    PRINT 'ok  : invoice ' + @number + ' created';
ELSE BEGIN PRINT 'FAIL: invoice not created as expected'; SET @failures += 1; END

/* 6. Void works while unpaid and returns the order to Fulfilled; then re-invoice. */
EXEC dbo.usp_Invoice_Void @InvoiceId = @invoiceId, @ChangedBy = N'smoke', @Comment = N'smoke void';

IF (SELECT Status FROM dbo.Orders WHERE Id = @orderId) = 'Fulfilled'
   AND (SELECT Status FROM dbo.Invoices WHERE Id = @invoiceId) = 'Void'
    PRINT 'ok  : void returned order to Fulfilled';
ELSE BEGIN PRINT 'FAIL: void did not behave'; SET @failures += 1; END

DECLARE @voidInvoiceId INT = @invoiceId;
EXEC dbo.usp_Invoice_CreateFromOrder @OrderId = @orderId, @CreatedBy = N'smoke', @IssuedDate = '2026-09-01', @InvoiceId = @invoiceId OUTPUT;

SELECT @number = InvoiceNumber FROM dbo.Invoices WHERE Id = @invoiceId;
IF @invoiceId <> @voidInvoiceId AND (SELECT Status FROM dbo.Orders WHERE Id = @orderId) = 'Invoiced'
    PRINT 'ok  : re-invoiced after void as ' + @number;
ELSE BEGIN PRINT 'FAIL: re-invoice after void failed'; SET @failures += 1; END

/* 7. Overpayment must fail with 50007. */
DECLARE @total DECIMAL(18,2), @tooMuch DECIMAL(18,2);
SELECT @total = Total FROM dbo.Invoices WHERE Id = @invoiceId;
SET @tooMuch = @total + 0.01;
BEGIN TRY
    EXEC dbo.usp_Invoice_ApplyPayment @InvoiceId = @invoiceId, @Amount = @tooMuch, @PaidDate = '2026-09-02', @Method = 'Cash', @CreatedBy = N'smoke', @PaymentId = @paymentId OUTPUT;
    PRINT 'FAIL: overpayment accepted';
    SET @failures += 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50007 PRINT 'ok  : overpayment rejected (50007)';
    ELSE BEGIN PRINT 'FAIL: unexpected error ' + CAST(ERROR_NUMBER() AS VARCHAR(10)); SET @failures += 1; END
END CATCH

/* 8. Partial payment, then void must fail with 50008. */
DECLARE @half DECIMAL(18,2) = ROUND(@total / 2, 2);
EXEC dbo.usp_Invoice_ApplyPayment @InvoiceId = @invoiceId, @Amount = @half, @PaidDate = '2026-09-02', @Method = 'BankTransfer', @CreatedBy = N'smoke', @Reference = N'SMOKE-1', @PaymentId = @paymentId OUTPUT;

IF EXISTS (SELECT 1 FROM dbo.Invoices WHERE Id = @invoiceId AND Status = 'PartiallyPaid' AND AmountPaid = @half)
    PRINT 'ok  : partial payment recorded';
ELSE BEGIN PRINT 'FAIL: partial payment state wrong'; SET @failures += 1; END

BEGIN TRY
    EXEC dbo.usp_Invoice_Void @InvoiceId = @invoiceId, @ChangedBy = N'smoke';
    PRINT 'FAIL: void accepted with payments present';
    SET @failures += 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50008 PRINT 'ok  : void with payments rejected (50008)';
    ELSE BEGIN PRINT 'FAIL: unexpected error ' + CAST(ERROR_NUMBER() AS VARCHAR(10)); SET @failures += 1; END
END CATCH

/* 9. Pay the rest; status must become Paid. */
DECLARE @rest DECIMAL(18,2) = @total - @half;
EXEC dbo.usp_Invoice_ApplyPayment @InvoiceId = @invoiceId, @Amount = @rest, @PaidDate = '2026-09-03', @Method = 'Card', @CreatedBy = N'smoke', @PaymentId = @paymentId OUTPUT;

IF EXISTS (SELECT 1 FROM dbo.Invoices WHERE Id = @invoiceId AND Status = 'Paid' AND AmountPaid = Total)
    PRINT 'ok  : invoice fully paid';
ELSE BEGIN PRINT 'FAIL: invoice not marked Paid'; SET @failures += 1; END

/* 10. Paying a paid invoice must fail with 50006. */
BEGIN TRY
    EXEC dbo.usp_Invoice_ApplyPayment @InvoiceId = @invoiceId, @Amount = 1, @PaidDate = '2026-09-03', @Method = 'Cash', @CreatedBy = N'smoke', @PaymentId = @paymentId OUTPUT;
    PRINT 'FAIL: payment on paid invoice accepted';
    SET @failures += 1;
END TRY
BEGIN CATCH
    IF ERROR_NUMBER() = 50006 PRINT 'ok  : payment on paid invoice rejected (50006)';
    ELSE BEGIN PRINT 'FAIL: unexpected error ' + CAST(ERROR_NUMBER() AS VARCHAR(10)); SET @failures += 1; END
END CATCH

/* 11. Status history should show the full chain. */
SELECT FromStatus, ToStatus, ChangedBy, Comment
  FROM dbo.OrderStatusHistory
 WHERE OrderId = @orderId
 ORDER BY ChangedAtUtc, Id;

/* ---------------------------------------------------------------------------
   Restore. Number series values consumed by the test are left as gaps,
   which is what would happen in production too.
   --------------------------------------------------------------------------- */
BEGIN TRANSACTION;

DELETE dbo.Payments WHERE InvoiceId IN (@invoiceId, @voidInvoiceId);
DELETE dbo.Invoices WHERE Id IN (@invoiceId, @voidInvoiceId);
DELETE dbo.OrderStatusHistory WHERE OrderId = @orderId AND ChangedBy = N'smoke';

UPDATE p
   SET QuantityOnHand = b.Qty
  FROM dbo.Products p
  JOIN @stockBefore b ON b.ProductId = p.Id;

UPDATE dbo.Orders
   SET Status = 'Approved', UpdatedAtUtc = @orderUpdatedAt, UpdatedBy = @orderUpdatedBy
 WHERE Id = @orderId;

COMMIT TRANSACTION;

PRINT CASE WHEN @failures = 0 THEN 'ALL OK (restored)' ELSE CAST(@failures AS VARCHAR(10)) + ' FAILURE(S) (restored)' END;
