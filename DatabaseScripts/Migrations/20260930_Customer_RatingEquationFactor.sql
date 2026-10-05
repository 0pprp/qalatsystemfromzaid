-- Per-customer rating equation factor (default 1).
-- Used only for virtual payment weighting in rating math — never mutates ledger amounts.
SET NOCOUNT ON;

IF COL_LENGTH(N'dbo.Customers', N'RatingEquationFactor') IS NULL
BEGIN
    ALTER TABLE dbo.Customers
        ADD RatingEquationFactor FLOAT NOT NULL
            CONSTRAINT DF_Customers_RatingEquationFactor DEFAULT (1);
END
GO
