

CREATE procEDURE [dbo].[InsertServerWithdrawalStores]
    @UserID INT = NULL,
    @State BIT = NULL,
    @WithdrawalStoresDate DATETIME= NULL,
    @StoreID INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[WithdrawalStores]
           ([UserID]
           ,[State]
           ,[WithdrawalStoresDate]
           ,[StoreID]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@State
           ,@WithdrawalStoresDate
           ,@StoreID
           ,@AsyncState
           ,@AsyncID)
END

