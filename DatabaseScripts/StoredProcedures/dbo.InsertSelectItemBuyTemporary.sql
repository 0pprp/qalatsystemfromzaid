CREATE proc [dbo].[InsertSelectItemBuyTemporary]
@ItemID int = NULL,
@Quantity int = NULL,
@UserID int = NULL

as

 
INSERT INTO [dbo].[SelectItemBuyTemporary]
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
 

