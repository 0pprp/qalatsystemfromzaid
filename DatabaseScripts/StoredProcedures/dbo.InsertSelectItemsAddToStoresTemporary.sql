CREATE proc [dbo].[InsertSelectItemsAddToStoresTemporary]
@ItemID int = NULL,
@Quantity int = NULL,
@UserID int  = NULL
as
 

INSERT INTO [dbo].[SelectItemsAddToStoresTemporary]
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
 


