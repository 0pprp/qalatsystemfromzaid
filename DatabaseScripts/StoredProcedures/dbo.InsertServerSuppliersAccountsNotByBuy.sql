CREATE procEDURE [dbo].[InsertServerSuppliersAccountsNotByBuy]
    @SupplierID INT = NULL,
    @UserID INT = NULL,
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
           ,[Amount]
           ,[AccountType]
           ,[AccountsDate]
           ,[AsyncState]
           ,[AsyncID] )
     VALUES
           (@SupplierID
           ,@UserID
           ,@Amount
           ,@AccountType
           ,@AccountsDate
           ,@AsyncState
           ,@AsyncID )
END

