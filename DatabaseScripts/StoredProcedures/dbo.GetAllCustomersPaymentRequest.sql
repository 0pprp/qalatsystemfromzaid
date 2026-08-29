CREATE proc [dbo].[GetAllCustomersPaymentRequest]
as
select * from View_CustomersPaymentsRequest order by CustomersPaymentsRequestID

