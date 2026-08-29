create proc [dbo].[CustomersPayments_Delete]
@CustomerPaymentID int,
@UserDeleteID int
as

exec DeleteAllAddToBoxAsyncID @CustomerPaymentID=@CustomerPaymentID
exec DeleteCustomersPaymentsAsyncID @CustomerPaymentID=@CustomerPaymentID
INSERT INTO [dbo].[Activities]
           ([UserID]
           ,[ActivityDescription]
           ,[ActivityDate]
           ,[AsyncState]
           ,[AsyncID])
     VALUES
           (@UserDeleteID
           ,N'تم حذف تسديد '+(select CONVERT(nvarchar(255),AmountDenar) from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+N' من العميل '+(select CustomerName from View_CustomersPayments where CustomerPaymentID=@CustomerPaymentID)+' '
           ,GETUTCDATE()
           ,'false'
           ,NEWID() )
 
delete from AddToBox where CustomerPaymentID=@CustomerPaymentID
delete from CustomersPayments where CustomerPaymentID=@CustomerPaymentID

