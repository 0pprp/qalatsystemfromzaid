CREATE proc [dbo].[DeleteCustomersSales]
@CustomerSaleID int = NULL,
@UserID int = NULL
as
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
 

