CREATE proc [dbo].[DeleteCustomersPaymentsAsyncID]
@CustomerPaymentID int = NULL
as
 Insert into DeleteData (CustomersPaymentsAsyncID) values ((select AsyncID from CustomersPayments where CustomerPaymentID=@CustomerPaymentID))

