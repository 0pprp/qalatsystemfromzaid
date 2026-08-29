CREATE proc [dbo].[DeleteBuy]
@BuyID int = NULL,
@UserID int = NULL
as

INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف المواد المشترية '+(select ItemsNames from View_Buys where BuyID=@BuyID)+N' من المورد '+(select SupplierName from View_Buys where BuyID=@BuyID)+N' من المخزن '+(select StoreName from View_Buys where BuyID=@BuyID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
exec RestoreItemQuantityBuy @BuyID=@BuyID
exec DeleteBuysItemsAsyncID @BuyID=@BuyID
exec DeleteSuppliersAccountsAsyncID @BuyID=@BuyID
exec DeleteWithdrawalFromBoxAsyncID @BuyID=@BuyID
exec DeleteBuysAsyncID @BuyID=@BuyID
delete from BuysItems where BuyID=@BuyID
delete from SuppliersAccounts where BuyID=@BuyID
delete from WithdrawalFromBox where BuyID=@BuyID
delete from Buys where BuyID=@BuyID

