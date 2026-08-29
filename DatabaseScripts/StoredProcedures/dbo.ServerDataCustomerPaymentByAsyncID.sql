CREATE proc [dbo].[ServerDataCustomerPaymentByAsyncID]
@AsyncID nvarchar(255) = null 
as
select top 1 * from CustomersPayments where AsyncID=@AsyncID

