CREATE procEDURE [dbo].[InsertServerSuppliersAccountsByBuy]
    @SupplierID INT = NULL,
    @UserID INT = NULL,
    @BuyID INT = NULL,
    @Amount FLOAT= NULL,
    @AccountType NVARCHAR(255)= NULL,
    @AccountsDate DATETIME= NULL,
    @AsyncState BIT = NULL,
    @AsyncID NVARCHAR(255) = NULL
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
           ,[AsyncID] )
     VALUES
           (@SupplierID
           ,@UserID
           ,@BuyID
           ,@Amount
           ,@AccountType
           ,@AccountsDate
           ,@AsyncState
           ,@AsyncID )
END

