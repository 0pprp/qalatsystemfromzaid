CREATE proc [dbo].[InsertCustomerSale]
@UserID int = NULL,
@CustomerID int = NULL,
@DateCreate datetime = NULL,
@StoreID int = NULL,
@DelegateID int = NULL,
@DiscountAmountTotal float = NULL,
@DiscountAmountTotalDay float  = NULL
as
INSERT INTO [dbo].[CustomersSales]
           ([UserID]
           ,[CustomerID]
           ,[DateCreate]
           ,[BoundNumber]
           ,[StoreID]
           ,[DelegateID]
           ,[AccountZero]
           ,[DelegateState]
           ,[DiscountAmountTotal]
           ,[DiscountAmountTotalDay]
           ,[AsyncState]
		   ,[AsyncID])
     VALUES
           (@UserID
           ,@CustomerID
           ,@DateCreate
           ,(select count(*)+1 from CustomersSales)
           ,@StoreID
           ,@DelegateID
           ,'false'
           ,'false'
           ,@DiscountAmountTotal
           ,@DiscountAmountTotalDay
           ,'false'
		   ,NEWID())
exec InsertWithdrawalStores @UserID=@UserID,@State='true',@WithdrawalStoresDate=@DateCreate,@StoreID=@StoreID
DECLARE @CustomerSaleID INT = (select top 1 CustomerSaleID from CustomersSales order by CustomerSaleID desc);
DECLARE @WithdrawalStoresID INT = (select top 1 WithdrawalStoresID from WithdrawalStores order by WithdrawalStoresID desc);
exec InsertSelectItemsSaleFromSelectItemSalesTemporary @CustomerSaleID=@CustomerSaleID,@WithdrawalStoresID=@WithdrawalStoresID,@UserID=@UserID
exec ClearSelectItemSalesTemporary @UserID=@UserID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم بيع '+(select  top 1 ItemsNames from View_CustomersSales order by CustomerSaleID desc)+N'  الى العميل '+(select  top 1 CustomerName from View_CustomersSales order by CustomerSaleID desc)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )

