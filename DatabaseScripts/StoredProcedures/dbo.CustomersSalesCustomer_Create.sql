create proc [dbo].[CustomersSalesCustomer_Create]
@CustomerName nvarchar(100),
@PhoneNumber nvarchar(100),
@Address nvarchar(100),
@ShopName nvarchar(100),
@NearestFunctionPoint nvarchar(100),
@SaleName nvarchar(100),
@ReceiptName nvarchar(100),
@Notes nvarchar(max),
@UserID int = NULL,
@DateCreate datetime = NULL,
@StoreID int = NULL,
@DelegateID int = NULL,
@DiscountAmountTotal float = NULL,
@DiscountAmountTotalDay float  = NULL
as
declare @DBName nvarchar(100) =  DB_NAME()
declare @CityID int = (SELECT dbo.GetCityID(@DBName) AS CityID)
insert into Customers (DelegateID,CityID,CustomerName,PhoneNumber,Address,ShopName,NearestFunctionPoint,SaleName,ReceiptName,Notes,UserID,AsyncID,AsyncState,CustomerState) values 
(@DelegateID,@CityID,@CustomerName,@PhoneNumber,@Address,@ShopName,@NearestFunctionPoint,@SaleName,@ReceiptName,@Notes,@UserID,NEWID(),0,1)
DECLARE @CustomerID int;
SET @CustomerID = IDENT_CURRENT('Customers');
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

DECLARE @CustomerSaleID int;
SET @CustomerSaleID = IDENT_CURRENT('CustomersSales');

DECLARE @WithdrawalStoresID int;
SET @WithdrawalStoresID = IDENT_CURRENT('WithdrawalStores');

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
select * from View_CustomersDelegate where  CustomerID = @CustomerID

