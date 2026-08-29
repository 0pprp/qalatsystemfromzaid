 

CREATE proc [dbo].[InsertBuy]
@UserID int = NULL,
@SupplierID int = NULL,
@DateCreate datetime,
@StoreID int = NULL,
@BoxID int = NULL,
@AmountPaidDenar float = NULL,
@FinalAmountTotalDenar float = NULL,
@AmountTotalDenar float = NULL,
@RemainingAmountDenar float = NULL
as

INSERT INTO [dbo].[Buys]
           ([UserID]
           ,[SupplierID]
           ,[BoundNumber]
           ,[DateCreate]
           ,[StoreID]
           ,[BoxID]
		   ,[AsyncID]
		   ,AsyncState
		   ,BuyState)
     VALUES
           (@UserID ,
@SupplierID ,
(select count(*)+1 from Buys) ,
@DateCreate ,
@StoreID ,
@BoxID,NEWID(),'false','true'  )
DECLARE @BuyID INT = (select top 1 BuyID from Buys order by BuyID desc);
DECLARE @Purpose nvarchar(max) = N'شراء '+(select top 1 ItemsNames from View_Buys order by BuyID desc)+'';
DECLARE @Notes nvarchar(max) = N'لا يوجد';
DECLARE @AmountAccount float = @FinalAmountTotalDenar-@AmountTotalDenar;
DECLARE @FromAccount nvarchar(255) = N'من حساب الخزينة '+(select BoxName from Boxes where BoxID=@BoxID)+'';
DECLARE @ToAccount nvarchar(255) = N'الى حساب المورد '+(select SupplierName from Suppliers where SupplierID=@SupplierID)+'';
exec InsertBuysItemsFromSelectItemBuyTemporary @BuyID=@BuyID,@UserID=@UserID
exec InsertWithdrawalFromBoxFromBuy @BoxID=@BoxID,@Amount=@AmountPaidDenar,@Purpose=@Purpose,@Notes=@Notes,@UserID=@UserID,@DateCreate=@DateCreate,@BuyID=@BuyID,@SupplierID=@SupplierID
exec InsertSuppliersAccount @SupplierID=@SupplierID,@UserID=@UserID,@BuyID=@BuyID,@Amount=@AmountAccount,@AccountType=N'لنا',@AccountsDate=@DateCreate 
exec InsertSuppliersAccount @SupplierID=@SupplierID,@UserID=@UserID,@BuyID=@BuyID,@Amount=@RemainingAmountDenar,@AccountType=N'علينا',@AccountsDate=@DateCreate 
exec InsertDocument @UserID=@UserID,@FromAccount=@FromAccount,@ToAccount=@ToAccount,@DocumentDateCreate=@DateCreate,@Notes=@Notes,@DocumentType=N'سند صرف',@Amount=@AmountPaidDenar
exec InsertAddToStore @UserID=@UserID, @DateAddToStore=@DateCreate, @StoreID=@StoreID 
DECLARE @AddToStoreID INT = (select top 1 AddToStoreID from AddToStores order by AddToStoreID desc);
exec InsertSelectItemsAddToStoresFromSelectItemBuyTemporary @AddToStoreID=@AddToStoreID,@UserID=@UserID
delete from SelectItemBuyTemporary where UserID=@UserID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم شراء العناصر '+(select  top 1 ItemsNames from View_Buys order by BuyID desc)+N' من المورد '+(select  top 1 SupplierName from View_Buys order by BuyID desc)+N' الى المخزن '+(select  top 1 StoreName from View_Buys order by BuyID desc)+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
  

