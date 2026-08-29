CREATE proc [dbo].[DeleteCustomersPaymentsRequest]
@CustomersPaymentsRequestID int = NULL
as
delete from CustomersPaymentsRequest where CustomersPaymentsRequestID=@CustomersPaymentsRequestID

