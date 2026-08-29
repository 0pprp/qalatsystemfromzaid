CREATE proc [dbo].[InsertSelectItemsAddToStores]
@AddToStoreID int = NULL,
@UserID int = NULL,
@ItemID int = NULL,
@Quantity int = NULL

as
 
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
           ,'false'
		   ,NEWID())
 

