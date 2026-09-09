/* ---------------------------------------------------------------------------
   0001  Schemas, sequences and number series
   Purpose : Create the rpt and auth schemas, the customer number sequence,
             and the NumberSeries table that hands out yearly order and
             invoice numbers.
   Date    : 2026-09-09
   Author  : Robert Dahl
   --------------------------------------------------------------------------- */

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'rpt')
    EXEC(N'CREATE SCHEMA rpt AUTHORIZATION dbo;');
GO

IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = N'auth')
    EXEC(N'CREATE SCHEMA auth AUTHORIZATION dbo;');
GO

/* Customer numbers are a single, never-resetting series: CUST-000001.
   A SEQUENCE is the cheapest way to get that; gaps from rolled-back
   transactions are acceptable for customer numbers. */
IF NOT EXISTS (SELECT 1 FROM sys.sequences WHERE name = N'seq_CustomerNumber' AND schema_id = SCHEMA_ID(N'dbo'))
    CREATE SEQUENCE dbo.seq_CustomerNumber
        AS INT
        START WITH 1
        INCREMENT BY 1
        MINVALUE 1
        NO CYCLE
        CACHE 20;
GO

/* Order and invoice numbers restart every year (ORD-2026-000001), which
   a SEQUENCE cannot do without DDL at runtime. A small table with one row
   per series and year, updated under a lock, does the job and never needs
   a schema change when the year rolls over. */
IF OBJECT_ID(N'dbo.NumberSeries', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.NumberSeries
    (
        SeriesCode   VARCHAR(10)  NOT NULL,
        SeriesYear   SMALLINT     NOT NULL,
        NextValue    INT          NOT NULL CONSTRAINT DF_NumberSeries_NextValue DEFAULT (1),
        UpdatedAtUtc DATETIME2(0) NOT NULL CONSTRAINT DF_NumberSeries_UpdatedAtUtc DEFAULT (SYSUTCDATETIME()),

        CONSTRAINT PK_NumberSeries PRIMARY KEY CLUSTERED (SeriesCode, SeriesYear),
        CONSTRAINT CK_NumberSeries_SeriesCode CHECK (SeriesCode IN ('ORD', 'INV')),
        CONSTRAINT CK_NumberSeries_NextValue CHECK (NextValue >= 1)
    );
END
GO
