 
CREATE proc [dbo].[CustomersSales_Delete]
@CustomerSaleID int = NULL,
@UserID int = NULL
as
declare @CustomerID int = (select top 1 CustomerID from CustomersSales where CustomerSaleID = @CustomerSaleID)
exec DeleteSelectItemsSalesAsyncID @CustomerSaleID=@CustomerSaleID
exec DeleteCustomersSalesAsyncID @CustomerSaleID=@CustomerSaleID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserID
           ,N'تم حذف المبيع '+(select ItemsNames from View_CustomersSales where CustomerSaleID=@CustomerSaleID)+N' التابع للعميل '+(select CustomerName from View_CustomersSales where CustomerSaleID=@CustomerSaleID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
exec RestoreItemQuantitySale @CustomerSaleID=@CustomerSaleID
delete from SelectItemsSales where  CustomerSaleID=@CustomerSaleID
delete from CustomersSales where  CustomerSaleID=@CustomerSaleID
update Customers set CustomerState=0 where CustomerID=@CustomerID
 

