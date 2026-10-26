-- Safe CustomersPaymentsApprove_Create: SCOPE_IDENTITY + transaction + serialized BoundNumber.
-- Isolated branch DB only. Do not apply to production without reconciliation.
CREATE OR ALTER PROC [dbo].[CustomersPaymentsApprove_Create]
@UserID  int = NULL,
@CustomerID  int = NULL,
@DateCreate datetime = NULL,
@Amount float = NULL
AS
BEGIN
  SET NOCOUNT ON;
  SET XACT_ABORT ON;
  BEGIN TRY
    BEGIN TRAN;

    DECLARE @DelegateID int = (SELECT DelegateID FROM Customers WHERE CustomerID=@CustomerID);
    DECLARE @AmountRemaining float = (SELECT AmountRemaining FROM CustomerZeroRemainingByDate WHERE CustomerID=@CustomerID);
    DECLARE @BoxID INT = (SELECT BoxID FROM Delegates WHERE DelegateID=@DelegateID);
    DECLARE @BoundNumber int =
      (SELECT ISNULL(MAX(BoundNumber), 0) + 1
       FROM dbo.CustomersPayments WITH (UPDLOCK, HOLDLOCK));

    INSERT INTO [dbo].[CustomersPayments]
    ([UserID],[CustomerID],[BoxID],[PaymentDate],[BoundNumber],[DelegateID],
     [AccountZero],[DelegateState],[Location],[AsyncState],[AsyncID])
    VALUES
    (@UserID,@CustomerID,@BoxID,@DateCreate,@BoundNumber,@DelegateID,
     'false','true','0000,0000','false',NEWID());

    DECLARE @LastId int = CAST(SCOPE_IDENTITY() AS int);
    IF @LastId IS NULL OR @LastId <= 0
      THROW 50011, N'CustomersPayments insert did not return identity.', 1;

    INSERT INTO [dbo].[AddToBox]
    ([BoxID],[Amount],[Notes],[UserID],[DateCreate],[CustomerPaymentID],[AsyncState],[AsyncID])
    VALUES
    (@BoxID,
     @Amount,
     N'قبض من الزبون '+(SELECT CustomerName FROM Customers WHERE CustomerID=@CustomerID)+'',
     @UserID,
     @DateCreate,
     @LastId,
     'false',
     NEWID());

    INSERT INTO [dbo].[Activities]
           ([UserID],[ActivityDescription],[ActivityDate],[AsyncState],[AsyncID])
     VALUES
           (@UserID,
            N'تم قبض من الزبون'+(SELECT CustomerName FROM Customers WHERE CustomerID=@CustomerID)+'',
            GETUTCDATE(),
            'false',
            NEWID());

    COMMIT TRAN;
  END TRY
  BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    THROW;
  END CATCH
END
GO
