-- Idempotent offline collection payments + 16:00 Baghdad posting fields.
-- Safe to re-run.

IF COL_LENGTH('dbo.CustomersPaymentsRequest', 'ClientPaymentId') IS NULL
    ALTER TABLE dbo.CustomersPaymentsRequest ADD ClientPaymentId NVARCHAR(36) NULL;
GO

IF COL_LENGTH('dbo.CustomersPaymentsRequest', 'CreatedAtUtc') IS NULL
    ALTER TABLE dbo.CustomersPaymentsRequest ADD CreatedAtUtc DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.CustomersPaymentsRequest', 'ReceivedAtUtc') IS NULL
    ALTER TABLE dbo.CustomersPaymentsRequest ADD ReceivedAtUtc DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.CustomersPaymentsRequest', 'EligibleForPostingAtUtc') IS NULL
    ALTER TABLE dbo.CustomersPaymentsRequest ADD EligibleForPostingAtUtc DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.CustomersPaymentsRequest', 'PostedToBoxAtUtc') IS NULL
    ALTER TABLE dbo.CustomersPaymentsRequest ADD PostedToBoxAtUtc DATETIME2 NULL;
GO

IF COL_LENGTH('dbo.CustomersPaymentsRequest', 'AutoPostEnabled') IS NULL
    ALTER TABLE dbo.CustomersPaymentsRequest ADD AutoPostEnabled BIT NOT NULL CONSTRAINT DF_CPR_AutoPostEnabled DEFAULT (0);
GO

IF COL_LENGTH('dbo.CustomersPaymentsRequest', 'ReceiptNumber') IS NULL
    ALTER TABLE dbo.CustomersPaymentsRequest ADD ReceiptNumber NVARCHAR(100) NULL;
GO

IF COL_LENGTH('dbo.CustomersPayments', 'ClientPaymentId') IS NULL
    ALTER TABLE dbo.CustomersPayments ADD ClientPaymentId NVARCHAR(36) NULL;
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_CustomersPaymentsRequest_ClientPaymentId'
      AND object_id = OBJECT_ID('dbo.CustomersPaymentsRequest'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_CustomersPaymentsRequest_ClientPaymentId
    ON dbo.CustomersPaymentsRequest(ClientPaymentId)
    WHERE ClientPaymentId IS NOT NULL;
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_CustomersPayments_ClientPaymentId'
      AND object_id = OBJECT_ID('dbo.CustomersPayments'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX UX_CustomersPayments_ClientPaymentId
    ON dbo.CustomersPayments(ClientPaymentId)
    WHERE ClientPaymentId IS NOT NULL;
END
GO

IF OBJECT_ID('dbo.CustomersPaymentsRequest_PostEligible', 'P') IS NOT NULL
    DROP PROCEDURE dbo.CustomersPaymentsRequest_PostEligible;
GO

CREATE PROCEDURE [dbo].[CustomersPaymentsRequest_PostEligible]
    @UserCreateID INT,
    @PostedCount INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET @PostedCount = 0;

    DECLARE @NowUtc DATETIME2 = SYSUTCDATETIME();
    DECLARE @CustomersPaymentsRequestID INT,
            @CustomerID INT,
            @Location NVARCHAR(255),
            @PaymentDate DATETIME,
            @Amount FLOAT,
            @AmountHash FLOAT,
            @AmountRemaining FLOAT,
            @ClientPaymentId NVARCHAR(36);

    DECLARE payment_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT
        cpr.CustomersPaymentsRequestID,
        cpr.CustomerID,
        cpr.Location,
        ISNULL(cpr.CreatedAtUtc, cpr.PaymentDate),
        cpr.Amount,
        (cpr.Amount / 1448.0) AS AmountHash,
        v.AmountRemaining,
        cpr.ClientPaymentId
    FROM CustomersPaymentsRequest cpr
    JOIN View_CustomersPaymentsRequestFinal v
        ON cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID
    WHERE cpr.AutoPostEnabled = 1
      AND cpr.PostedToBoxAtUtc IS NULL
      AND cpr.EligibleForPostingAtUtc IS NOT NULL
      AND cpr.EligibleForPostingAtUtc <= @NowUtc
      AND v.AmountRemaining > 0
      AND v.AmountRemaining >= cpr.Amount
    ORDER BY cpr.EligibleForPostingAtUtc ASC, cpr.CustomersPaymentsRequestID ASC;

    OPEN payment_cursor;
    FETCH NEXT FROM payment_cursor
    INTO @CustomersPaymentsRequestID, @CustomerID, @Location, @PaymentDate, @Amount, @AmountHash, @AmountRemaining, @ClientPaymentId;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;

            IF @ClientPaymentId IS NOT NULL
               AND EXISTS (SELECT 1 FROM CustomersPayments WHERE ClientPaymentId = @ClientPaymentId)
            BEGIN
                UPDATE CustomersPaymentsRequest
                SET PostedToBoxAtUtc = @NowUtc, UpdatedDate = GETDATE()
                WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;

                DELETE FROM CustomersPaymentsRequest
                WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;

                SET @PostedCount = @PostedCount + 1;
                COMMIT TRANSACTION;
            END
            ELSE IF @AmountRemaining > 0 AND @AmountRemaining >= @Amount
            BEGIN
                EXEC CustomersPaymentsFromRequest_Create
                    @UserID = @UserCreateID,
                    @CustomerID = @CustomerID,
                    @DateCreate = @PaymentDate,
                    @Amount = @AmountHash,
                    @Location = @Location;

                IF @ClientPaymentId IS NOT NULL
                BEGIN
                    UPDATE cp
                    SET ClientPaymentId = @ClientPaymentId
                    FROM CustomersPayments cp
                    WHERE cp.CustomerPaymentID = (
                        SELECT TOP 1 CustomerPaymentID
                        FROM CustomersPayments
                        WHERE CustomerID = @CustomerID
                        ORDER BY CustomerPaymentID DESC
                    )
                    AND cp.ClientPaymentId IS NULL;
                END

                UPDATE CustomersPaymentsRequest
                SET PostedToBoxAtUtc = @NowUtc, UpdatedDate = GETDATE()
                WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;

                DELETE FROM CustomersPaymentsRequest
                WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;

                SET @PostedCount = @PostedCount + 1;
                COMMIT TRANSACTION;
            END
            ELSE
            BEGIN
                ROLLBACK TRANSACTION;
            END
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        END CATCH;

        FETCH NEXT FROM payment_cursor
        INTO @CustomersPaymentsRequestID, @CustomerID, @Location, @PaymentDate, @Amount, @AmountHash, @AmountRemaining, @ClientPaymentId;
    END;

    CLOSE payment_cursor;
    DEALLOCATE payment_cursor;
END
GO
