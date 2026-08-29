

CREATE procEDURE [dbo].[InsertServerWithdrawalFromBox]
    @BoxID INT = NULL,
    @Amount FLOAT= NULL,
    @Purpose NVARCHAR(MAX)= NULL,
    @Notes NVARCHAR(MAX) = NULL,
    @UserID INT = NULL,
    @DateCreate DATETIME NULL,
    @DateModify datetime =NULL,
    @CustomerID INT = NULL,
    @EmployeeID INT = NULL,
    @DelegateID INT = NULL,
    @DocumentID INT = NULL,
    @ExchangeItemID INT = NULL,
    @TransferBoxID INT = NULL,
    @BuyID INT = NULL,
    @SupplierID INT = NULL,
    @DelegateSalaryID INT = NULL,
    @EmployeeSalaryID INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @BuyBalanceID int = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[WithdrawalFromBox]
           ([BoxID]
           ,[Amount]
           ,[Purpose]
           ,[Notes]
           ,[UserID]
           ,[DateCreate]
           ,[DateModify]
           ,[CustomerID]
           ,[EmployeeID]
           ,[DelegateID]
           ,[DocumentID]
           ,[ExchangeItemID]
           ,[TransferBoxID]
           ,[BuyID]
           ,[SupplierID]
           ,[DelegateSalaryID]
           ,[EmployeeSalaryID]
           ,[AsyncState]
           ,[AsyncID]
           ,[BuyBalanceID])
     VALUES
           (@BoxID
           ,@Amount
           ,@Purpose
           ,@Notes
           ,@UserID
           ,@DateCreate
           ,@DateModify
           ,@CustomerID
           ,@EmployeeID
           ,@DelegateID
           ,@DocumentID
           ,@ExchangeItemID
           ,@TransferBoxID
           ,@BuyID
           ,@SupplierID
           ,@DelegateSalaryID
           ,@EmployeeSalaryID
           ,@AsyncState
           ,@AsyncID
           ,@BuyBalanceID)
END

