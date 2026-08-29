

CREATE procEDURE [dbo].[InsertServerAddToStores]
    @UserID INT = NULL,
    @DateAddToStore DATETIME = NULL,
    @StoreID INT = NULL,
    @State BIT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[AddToStores]
           ([UserID]
           ,[DateAddToStore]
           ,[StoreID]
           ,[State]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,@DateAddToStore
           ,@StoreID
           ,@State
           ,@AsyncState
           ,@AsyncID)
END

