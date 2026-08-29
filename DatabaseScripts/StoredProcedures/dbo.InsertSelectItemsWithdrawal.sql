CREATE proc [dbo].[InsertSelectItemsWithdrawal]
@WithdrawalStoresID  int = NULL, 
@UserID  int = NULL, 
@ItemID  int = NULL, 
@Quantity  int  = NULL
as
 
INSERT INTO [dbo].[SelectItemsWithdrawal]
           ([WithdrawalStoresID]
           ,[UserID]
           ,[ItemID]
           ,[Quantity]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@WithdrawalStoresID 
           ,@UserID 
           ,@ItemID 
           ,@Quantity 
           ,'false'
		   ,NEWID())
 

