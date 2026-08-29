CREATE proc [dbo].[InsertSelectItemsTransferStores]
@TransferStoreID int = NULL,
@UserID int = NULL,
@ItemID int = NULL,
@Quantity int = NULL
as
 
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
           ,'false'
		   ,NEWID())
  

