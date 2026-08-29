CREATE proc [dbo].[InsertWithdrawalStores]
@UserID int = NULL,
@State bit= NULL,
@WithdrawalStoresDate datetime= NULL,
@StoreID int = NULL
as
 
INSERT INTO [dbo].[WithdrawalStores]
           ([UserID]
           ,[State]
           ,[WithdrawalStoresDate]
           ,[StoreID]
           ,[AsyncState] 
		    ,[AsyncID])
     VALUES
           (@UserID 
           ,@State 
           ,@WithdrawalStoresDate
           ,@StoreID 
           ,'false'
		   ,NEWID())
 

