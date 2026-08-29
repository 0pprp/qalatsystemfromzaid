

CREATE procEDURE [dbo].[InsertServerSuppliersAccounts]
    @SupplierID INT = NULL,
    @UserID INT = NULL,
    @BuyID INT= NULL,
    @Amount FLOAT= NULL,
    @AccountType NVARCHAR(255)= NULL,
    @AccountsDate DATETIME= NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255)= NULL,
    @BuyBalanceID INT= NULL
AS
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[SuppliersAccounts]
           ([SupplierID]
           ,[UserID]
           ,[BuyID]
           ,[Amount]
           ,[AccountType]
           ,[AccountsDate]
           ,[AsyncState]
           ,[AsyncID]
           ,[BuyBalanceID])
     VALUES
           (@SupplierID
           ,@UserID
           ,@BuyID
           ,@Amount
           ,@AccountType
           ,@AccountsDate
           ,@AsyncState
           ,@AsyncID
           ,@BuyBalanceID)
END

