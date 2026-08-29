 
CREATE proc [dbo].[GetAllCustomersPaymentRequestByCustomerName]
@CustomerName nvarchar(255)
as
select * from View_CustomersPaymentsRequest
where CustomerName like N'%'+@CustomerName+N'%' order by CustomersPaymentsRequestID

