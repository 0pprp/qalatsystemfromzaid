CREATE procEDURE [dbo].[InsertServerAddToBox]
    @BoxID INT = NULL,
    @Amount FLOAT  = NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @UserID INT = NULL,
    @SupplierID INT = NULL,
    @DelegateID INT = NULL,
    @DateCreate DATETIME = NULL,
    @DateModify datetime =NULL,
    @CustomerPaymentID INT = NULL,
    @EmployeeID INT = NULL,
    @DocumentID INT = NULL,
    @CustomerID INT = NULL,
    @TransferBoxID INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL ,
    @CustomerPaymentBalanceID int = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[AddToBox]
           ([BoxID]
           ,[Amount]
           ,[Notes]
           ,[UserID]
           ,[SupplierID]
           ,[DelegateID]
           ,[DateCreate]
           ,[DateModify]
           ,[CustomerPaymentID]
           ,[EmployeeID]
           ,[DocumentID]
           ,[CustomerID]
           ,[TransferBoxID]
           ,[AsyncState]
           ,[AsyncID]
           ,[CustomerPaymentBalanceID])
     VALUES
           (@BoxID
           ,@Amount
           ,@Notes
           ,@UserID
           ,@SupplierID
           ,@DelegateID
           ,@DateCreate
           ,@DateModify
           ,@CustomerPaymentID
           ,@EmployeeID
           ,@DocumentID
           ,@CustomerID
           ,@TransferBoxID
           ,@AsyncState
           ,@AsyncID
           ,@CustomerPaymentBalanceID)
END

