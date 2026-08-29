CREATE PROCEDURE [dbo].[CustomersPaymentsRequest_ApproveAll]  
    @UserCreateID INT
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @DatabaseName NVARCHAR(100) = DB_NAME();
    EXEC BackupDatabaseAndPayment @DatabaseName = @DatabaseName;

    DECLARE @CustomersPaymentsRequestID INT,
            @CustomerID INT,
            @Location NVARCHAR(100),
            @PaymentDate DATETIME,
            @Amount FLOAT,
            @AmountHash FLOAT,
            @AmountRemaining FLOAT;

    DECLARE payment_cursor CURSOR LOCAL FAST_FORWARD FOR
    SELECT 
        cpr.CustomersPaymentsRequestID,
        cpr.CustomerID,
        cpr.Location,               
        cpr.PaymentDate,
        cpr.Amount,
        (cpr.Amount / 1448.0) AS AmountHash,
        v.AmountRemaining
    FROM CustomersPaymentsRequest cpr
    JOIN View_CustomersPaymentsRequestFinal v 
        ON cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID
    WHERE v.AmountRemaining > 0 AND v.AmountRemaining >= cpr.Amount;

    OPEN payment_cursor;

    FETCH NEXT FROM payment_cursor  
    INTO @CustomersPaymentsRequestID, @CustomerID, @Location, @PaymentDate, @Amount, @AmountHash, @AmountRemaining;

    WHILE @@FETCH_STATUS = 0
    BEGIN
        IF @AmountRemaining > 0 AND @AmountRemaining >= @Amount
        BEGIN
            EXEC CustomersPaymentsFromRequest_Create 
                @UserID = @UserCreateID,
                @CustomerID = @CustomerID, 
                @DateCreate = @PaymentDate,
                @Amount = @AmountHash,
                @Location = @Location;

            DELETE FROM CustomersPaymentsRequest 
            WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;
        END;

        FETCH NEXT FROM payment_cursor  
        INTO @CustomersPaymentsRequestID, @CustomerID, @Location, @PaymentDate, @Amount, @AmountHash, @AmountRemaining;
    END;

    CLOSE payment_cursor;
    DEALLOCATE payment_cursor;
END;

