/* ---------------------------------------------------------------------------
   0007  Numbering procedures
   Purpose : Hand out public-facing document numbers. Customer numbers come
             from a sequence; order and invoice numbers come from the
             per-year NumberSeries table. C# never formats these itself.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

CREATE OR ALTER PROCEDURE dbo.usp_NextCustomerNumber
    @CustomerNumber VARCHAR(12) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @value INT = NEXT VALUE FOR dbo.seq_CustomerNumber;

    IF @value > 999999
        THROW 50000, 'Customer number series is exhausted.', 1;

    SET @CustomerNumber = 'CUST-' + RIGHT('000000' + CAST(@value AS VARCHAR(10)), 6);
END
GO

/* Returns the next number in a yearly series, e.g. ORD-2026-000042.

   Concurrency: the existence check takes an update lock with a key-range
   lock (HOLDLOCK) inside a transaction, so two callers for the same series
   and year queue behind each other. The first caller in a new year inserts
   the row; the second waits on the range lock, then finds the row. No
   error path, which matters because callers run with XACT_ABORT ON. */
CREATE OR ALTER PROCEDURE dbo.usp_NextSeriesNumber
    @SeriesCode VARCHAR(10),
    @SeriesYear SMALLINT,
    @Number     VARCHAR(16) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @value INT;

    BEGIN TRY
        BEGIN TRANSACTION;

        IF NOT EXISTS (SELECT 1 FROM dbo.NumberSeries WITH (UPDLOCK, HOLDLOCK)
                        WHERE SeriesCode = @SeriesCode AND SeriesYear = @SeriesYear)
        BEGIN
            INSERT dbo.NumberSeries (SeriesCode, SeriesYear, NextValue)
            VALUES (@SeriesCode, @SeriesYear, 1);
        END

        /* All right-hand references see the row as it was before the update,
           so @value receives the number being handed out. */
        UPDATE dbo.NumberSeries
           SET @value       = NextValue,
               NextValue    = NextValue + 1,
               UpdatedAtUtc = SYSUTCDATETIME()
         WHERE SeriesCode = @SeriesCode
           AND SeriesYear = @SeriesYear;

        COMMIT TRANSACTION;
    END TRY
    BEGIN CATCH
        IF @@TRANCOUNT > 0
            ROLLBACK TRANSACTION;
        THROW;
    END CATCH

    IF @value > 999999
        THROW 50000, 'Number series is exhausted for this year.', 1;

    SET @Number = @SeriesCode + '-' + CAST(@SeriesYear AS VARCHAR(4)) + '-' + RIGHT('000000' + CAST(@value AS VARCHAR(10)), 6);
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_NextOrderNumber
    @SeriesYear  SMALLINT    = NULL,
    @OrderNumber VARCHAR(16) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @SeriesYear = ISNULL(@SeriesYear, YEAR(SYSUTCDATETIME()));

    EXEC dbo.usp_NextSeriesNumber @SeriesCode = 'ORD', @SeriesYear = @SeriesYear, @Number = @OrderNumber OUTPUT;
END
GO

CREATE OR ALTER PROCEDURE dbo.usp_NextInvoiceNumber
    @SeriesYear    SMALLINT    = NULL,
    @InvoiceNumber VARCHAR(16) OUTPUT
AS
BEGIN
    SET NOCOUNT ON;

    SET @SeriesYear = ISNULL(@SeriesYear, YEAR(SYSUTCDATETIME()));

    EXEC dbo.usp_NextSeriesNumber @SeriesCode = 'INV', @SeriesYear = @SeriesYear, @Number = @InvoiceNumber OUTPUT;
END
GO
