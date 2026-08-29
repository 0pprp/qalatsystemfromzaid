CREATE proc [dbo].[ServerDataCustomersPaymentByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from CustomersPayments where AsyncID=@AsyncID

