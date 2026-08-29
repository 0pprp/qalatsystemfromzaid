CREATE proc [dbo].[InsertSelectItemDamageTemporary]
@ItemID int = NULL,
@Quantity int = NULL,
@UserID int = NULL
 
as
 

INSERT INTO [dbo].[SelectItemDamageTemporary]
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
 

