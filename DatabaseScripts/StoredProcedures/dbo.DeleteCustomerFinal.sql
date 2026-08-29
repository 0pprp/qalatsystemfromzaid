 
CREATE proc [dbo].[DeleteCustomerFinal]
@CustomerID int = NULL,
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
           ,N'تم حذف العميل '+(select CustomerName from Customers where CustomerID=@CustomerID)+N' بشكل نهائي معرفة '+CONVERT(nvarchar(255),@CustomerID)+''
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
exec DeleteWithdrawalFromBoxCustomerAsyncID @CustomerID=@CustomerID
exec DeleteAllPaymentCustomer @CustomerID=@CustomerID , @UserID=@UserID
exec DeleteAllSalesCustomer @CustomerID=@CustomerID , @UserID=@UserID
delete from AddToBox where CustomerID=@CustomerID
delete from CustomerOldRequest where CustomerID=@CustomerID
delete from CustomerPaymentBalance where CustomerID=@CustomerID
delete from CustomerPaymentBalanceRequest where CustomerID=@CustomerID
delete from CustomerSaleBalance where CustomerID=@CustomerID
delete from CustomerSaleBalanceRequestOld where CustomerID=@CustomerID
delete from CustomersPaymentsRequest where CustomerID=@CustomerID
delete from CustomersSalesRequestOld where CustomerID=@CustomerID
delete from SelectItemCustomerTemp where CustomerID=@CustomerID
delete from SelectPaymentCustomerTemporary where CustomerID=@CustomerID
delete from CustomersSalesRequestOld where CustomerID=@CustomerID
delete from CustomerPaymentBalanceRequest where CustomerID=@CustomerID
delete from CustomerNotification where CustomerID=@CustomerID
delete from WithdrawalFromBox where CustomerID=@CustomerID
delete from Customers where CustomerID=@CustomerID

