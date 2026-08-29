CREATE proc [dbo].[InsertSelectItemSalesTemporary]
@ItemID int = NULL,
@UserID int = NULL,
@Quantity int = NULL
as
INSERT INTO [dbo].[SelectItemSalesTemporary]
           ([ItemID]
           ,[UserID]
           ,[Quantity]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@ItemID 
           ,@UserID 
           ,@Quantity 
           ,'false' 
		   ,NEWID())
 

