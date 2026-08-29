CREATE proc [dbo].[InsertSelectItemsTransferStoresTemporary]
@ItemID  int = NULL, 
@Quantity  int = NULL, 
@UserID  int = NULL
as
 
INSERT INTO [dbo].[SelectItemsTransferStoresTemporary]
           ([ItemID]
           ,[Quantity]
           ,[UserID]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@ItemID 
           ,@Quantity 
           ,@UserID 
           ,'false'
		   ,NEWID())
 

