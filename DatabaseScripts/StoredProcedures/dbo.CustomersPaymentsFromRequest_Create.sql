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
