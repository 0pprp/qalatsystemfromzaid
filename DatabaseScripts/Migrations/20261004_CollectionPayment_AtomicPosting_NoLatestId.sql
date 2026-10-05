SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- Atomic collection posting: claim request row, INSERT CustomersPayments with OUTPUT,
-- attach AddToBox to THAT id, set ClientPaymentId in the same INSERT.
-- Removes the previous latest-global-id race.
-- Safe to re-run.

IF OBJECT_ID('dbo.CustomersPaymentsFromRequest_Create', 'P') IS NOT NULL
    DROP PROCEDURE dbo.CustomersPaymentsFromRequest_Create;
GO

CREATE PROCEDURE [dbo].[CustomersPaymentsFromRequest_Create]
    @UserID INT = NULL,
    @CustomerID INT = NULL,
    @DateCreate DATETIME = NULL,
    @Amount FLOAT = NULL,
    @Location NVARCHAR(100) = NULL,
    @ClientPaymentId NVARCHAR(36) = NULL,
    @NewCustomerPaymentID INT = NULL OUTPUT
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @DelegateID INT = (SELECT DelegateID FROM dbo.Customers WHERE CustomerID = @CustomerID);
    DECLARE @BoxID INT = (SELECT BoxID FROM dbo.Delegates WHERE DelegateID = @DelegateID);
    DECLARE @BoundNumber INT = (
        SELECT COUNT(*) + 1 FROM dbo.CustomersPayments WHERE DelegateID = @DelegateID
    );

    -- Idempotent: if already posted under this ClientPaymentId, return existing id.
    IF @ClientPaymentId IS NOT NULL
       AND EXISTS (SELECT 1 FROM dbo.CustomersPayments WHERE ClientPaymentId = @ClientPaymentId)
    BEGIN
        SELECT @NewCustomerPaymentID = CustomerPaymentID
        FROM dbo.CustomersPayments
        WHERE ClientPaymentId = @ClientPaymentId;

        SELECT * FROM dbo.View_CustomersPayments WHERE CustomerPaymentID = @NewCustomerPaymentID;
        RETURN;
    END

    DECLARE @Inserted TABLE (CustomerPaymentID INT NOT NULL);

    INSERT INTO dbo.CustomersPayments
    (
        UserID, CustomerID, BoxID, PaymentDate, BoundNumber, DelegateID,
        AccountZero, DelegateState, Location, AsyncState, AsyncID, ClientPaymentId
    )
    OUTPUT INSERTED.CustomerPaymentID INTO @Inserted(CustomerPaymentID)
    VALUES
    (
        @UserID,
        @CustomerID,
        @BoxID,
        @DateCreate,
        @BoundNumber,
        @DelegateID,
        'false',
        'true',
        @Location,
        'false',
        NEWID(),
        @ClientPaymentId
    );

    SELECT @NewCustomerPaymentID = CustomerPaymentID FROM @Inserted;

    INSERT INTO dbo.AddToBox
    (
        BoxID, Amount, Notes, UserID, DateCreate, CustomerPaymentID, AsyncState, AsyncID
    )
    VALUES
    (
        @BoxID,
        @Amount,
        N'قبض من الزبون ' + (SELECT CustomerName FROM dbo.Customers WHERE CustomerID = @CustomerID),
        @UserID,
        @DateCreate,
        @NewCustomerPaymentID,
        'false',
        NEWID()
    );

    INSERT INTO dbo.Activities
    (
        UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID
    )
    VALUES
    (
        @UserID,
        N'تم اضافة المبلغ ' + CONVERT(NVARCHAR(255), @Amount * 1448)
            + N' من الزبون ' + (SELECT CustomerName FROM dbo.Customers WHERE CustomerID = @CustomerID)
            + N' الى الخزينة ' + (SELECT BoxName FROM dbo.Boxes WHERE BoxID = @BoxID),
        GETUTCDATE(),
        'false',
        NEWID()
    );

    SELECT * FROM dbo.View_CustomersPayments WHERE CustomerPaymentID = @NewCustomerPaymentID;
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
    SET XACT_ABORT ON;
    SET @PostedCount = 0;

    DECLARE @NowUtc DATETIME2 = SYSUTCDATETIME();
    DECLARE @CustomersPaymentsRequestID INT,
            @CustomerID INT,
            @Location NVARCHAR(255),
            @PaymentDate DATETIME,
            @Amount FLOAT,
            @AmountHash FLOAT,
            @AmountRemaining FLOAT,
            @ClientPaymentId NVARCHAR(36),
            @NewPaymentID INT,
            @Claimed INT;

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
    FROM dbo.CustomersPaymentsRequest cpr
    JOIN dbo.View_CustomersPaymentsRequestFinal v
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
    INTO @CustomersPaymentsRequestID, @CustomerID, @Location, @PaymentDate,
         @Amount, @AmountHash, @AmountRemaining, @ClientPaymentId;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        BEGIN TRY
            BEGIN TRANSACTION;

            -- Atomic claim: only one worker may own this request.
            UPDATE dbo.CustomersPaymentsRequest WITH (ROWLOCK, UPDLOCK, READPAST)
            SET PostedToBoxAtUtc = @NowUtc,
                UpdatedDate = GETDATE()
            WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID
              AND PostedToBoxAtUtc IS NULL;

            SET @Claimed = @@ROWCOUNT;
            IF @Claimed = 0
            BEGIN
                ROLLBACK TRANSACTION;
                GOTO NextPayment;
            END

            IF @ClientPaymentId IS NOT NULL
               AND EXISTS (SELECT 1 FROM dbo.CustomersPayments WHERE ClientPaymentId = @ClientPaymentId)
            BEGIN
                DELETE FROM dbo.CustomersPaymentsRequest
                WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;
                SET @PostedCount = @PostedCount + 1;
                COMMIT TRANSACTION;
                GOTO NextPayment;
            END

            IF @AmountRemaining > 0 AND @AmountRemaining >= @Amount
            BEGIN
                EXEC dbo.CustomersPaymentsFromRequest_Create
                    @UserID = @UserCreateID,
                    @CustomerID = @CustomerID,
                    @DateCreate = @PaymentDate,
                    @Amount = @AmountHash,
                    @Location = @Location,
                    @ClientPaymentId = @ClientPaymentId,
                    @NewCustomerPaymentID = @NewPaymentID OUTPUT;

                IF @NewPaymentID IS NULL
                BEGIN
                    -- Unclaim so a later sweep can retry.
                    UPDATE dbo.CustomersPaymentsRequest
                    SET PostedToBoxAtUtc = NULL
                    WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;
                    ROLLBACK TRANSACTION;
                    GOTO NextPayment;
                END

                DELETE FROM dbo.CustomersPaymentsRequest
                WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;

                SET @PostedCount = @PostedCount + 1;
                COMMIT TRANSACTION;
            END
            ELSE
            BEGIN
                UPDATE dbo.CustomersPaymentsRequest
                SET PostedToBoxAtUtc = NULL
                WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;
                ROLLBACK TRANSACTION;
            END
        END TRY
        BEGIN CATCH
            IF @@TRANCOUNT > 0 ROLLBACK TRANSACTION;
        END CATCH;

NextPayment:
        FETCH NEXT FROM payment_cursor
        INTO @CustomersPaymentsRequestID, @CustomerID, @Location, @PaymentDate,
             @Amount, @AmountHash, @AmountRemaining, @ClientPaymentId;
    END;

    CLOSE payment_cursor;
    DEALLOCATE payment_cursor;
END
GO

-- Keep uniqueness guarantees for concurrent inserts.
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
