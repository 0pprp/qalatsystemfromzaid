

CREATE procEDURE [dbo].[InsertServerSelectItemsTransferStores]
    @TransferStoreID INT = NULL,
    @UserID INT = NULL,
    @ItemID INT = NULL,
    @Quantity INT = NULL,
    @AsyncState BIT NULL,
    @AsyncID nvarchar(255) = NULL
as
BEGIN
    SET NOCOUNT ON;

    INSERT INTO [dbo].[SelectItemsTransferStores]
           ([TransferStoreID]
           ,[UserID]
           ,[ItemID]
           ,[Quantity]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@TransferStoreID
           ,@UserID
           ,@ItemID
           ,@Quantity
           ,@AsyncState
           ,@AsyncID)
END

