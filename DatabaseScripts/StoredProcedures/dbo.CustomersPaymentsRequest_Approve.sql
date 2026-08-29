
CREATE PROCEDURE [dbo].[CustomersPaymentsRequest_Approve]
    @CustomersPaymentsRequestID INT,
    @UserCreateID INT
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE  @CustomerID INT,  @DateCreate DATETIME, @Amount FLOAT, @AmountHash FLOAT,  @AmountRemaining FLOAT;
    SELECT 
        @CustomerID    = cpr.CustomerID,
        @DateCreate    = cpr.PaymentDate,
        @Amount        = cpr.Amount,
        @AmountHash    = cpr.Amount / 1448.0,
        @AmountRemaining = v.AmountRemaining
    FROM CustomersPaymentsRequest cpr JOIN View_CustomersPaymentsRequestFinal v ON cpr.CustomersPaymentsRequestID = v.CustomersPaymentsRequestID WHERE cpr.CustomersPaymentsRequestID = @CustomersPaymentsRequestID;
    IF (@AmountRemaining > 0 AND @AmountRemaining >= @Amount)
    BEGIN
        EXEC CustomersPayments_Create 
            @UserID = @UserCreateID,
            @CustomerID = @CustomerID,
            @DateCreate = @DateCreate,
            @Amount = @AmountHash;
        DELETE FROM CustomersPaymentsRequest 
        WHERE CustomersPaymentsRequestID = @CustomersPaymentsRequestID;
    END;
END;


