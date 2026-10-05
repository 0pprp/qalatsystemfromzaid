SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO
-- Fix CustomersPayments_Create: stop resolving new payment via global TOP 1 ORDER BY DESC.
-- Use OUTPUT INSERTED.CustomerPaymentID for AddToBox association.
-- Safe to re-run.

IF OBJECT_ID(N'dbo.CustomersPayments_Create', N'P') IS NOT NULL
    DROP PROCEDURE dbo.CustomersPayments_Create;
GO

CREATE PROC [dbo].[CustomersPayments_Create]
    @UserID INT = NULL,
    @CustomerID INT = NULL,
    @DateCreate DATETIME = NULL,
    @Amount FLOAT = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SET XACT_ABORT ON;

    DECLARE @DelegateID INT = (SELECT DelegateID FROM dbo.Customers WHERE CustomerID = @CustomerID);
    DECLARE @BoxID INT = (SELECT BoxID FROM dbo.Delegates WHERE DelegateID = @DelegateID);
    DECLARE @Inserted TABLE (CustomerPaymentID INT NOT NULL);

    INSERT INTO [dbo].[CustomersPayments]
    (
        [UserID],
        [CustomerID],
        [BoxID],
        [PaymentDate],
        [BoundNumber],
        [DelegateID],
        [AccountZero],
        [DelegateState],
        [Location],
        [AsyncState],
        [AsyncID]
    )
    OUTPUT INSERTED.CustomerPaymentID INTO @Inserted(CustomerPaymentID)
    VALUES
    (
        @UserID,
        @CustomerID,
        @BoxID,
        @DateCreate,
        (SELECT COUNT(*) + 1 FROM dbo.CustomersPayments),
        @DelegateID,
        'false',
        'true',
        '0000,0000',
        'false',
        NEWID()
    );

    DECLARE @NewId INT = (SELECT CustomerPaymentID FROM @Inserted);

    INSERT INTO [dbo].[AddToBox]
    (
        [BoxID],
        [Amount],
        [Notes],
        [UserID],
        [DateCreate],
        [CustomerPaymentID],
        [AsyncState],
        [AsyncID]
    )
    VALUES
    (
        @BoxID,
        @Amount,
        N'قبض من الزبون ' + (SELECT CustomerName FROM dbo.Customers WHERE CustomerID = @CustomerID),
        @UserID,
        @DateCreate,
        @NewId,
        'false',
        NEWID()
    );

    INSERT INTO [dbo].[Activities]
    (
        [UserID],
        [ActivityDescription],
        [ActivityDate],
        [AsyncState],
        [AsyncID]
    )
    VALUES
    (
        @UserID,
        N'تم اضافة المبلغ ' + CONVERT(NVARCHAR(255), @Amount * 1448) + N' من الزبون '
            + (SELECT CustomerName FROM dbo.Customers WHERE CustomerID = @CustomerID)
            + N' الى الخزينة ' + (SELECT BoxName FROM dbo.Boxes WHERE BoxID = @BoxID),
        GETUTCDATE(),
        'false',
        NEWID()
    );

    SELECT * FROM dbo.View_CustomersPayments WHERE CustomerPaymentID = @NewId;
END
GO
