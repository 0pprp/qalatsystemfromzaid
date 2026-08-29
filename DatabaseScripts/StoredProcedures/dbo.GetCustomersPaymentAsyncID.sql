CREATE proc [dbo].[GetCustomersPaymentAsyncID]
@CustomerPaymentID int = NULL
as
select AsyncID from CustomersPayments where CustomerPaymentID=@CustomerPaymentID

