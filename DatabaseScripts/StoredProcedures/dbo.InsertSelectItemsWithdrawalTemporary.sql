CREATE proc [dbo].[InsertSelectItemsWithdrawalTemporary]
@ItemID int = NULL, 
@Quantity int = NULL, 
@UserID int  = NULL
as
 
INSERT INTO [dbo].[SelectItemsWithdrawalTemporary]
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
 

