

CREATE procEDURE [dbo].[InsertServerSelectItemsAddToStores]
    @AddToStoreID INT = NULL,
    @UserID INT = NULL,
    @ItemID INT = NULL,
    @Quantity INT = NULL,
    @AsyncState BIT = NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[SelectItemsAddToStores]
           ([AddToStoreID]
           ,[UserID]
           ,[ItemID]
           ,[Quantity]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@AddToStoreID
           ,@UserID
           ,@ItemID
           ,@Quantity
           ,@AsyncState
           ,@AsyncID)
END

