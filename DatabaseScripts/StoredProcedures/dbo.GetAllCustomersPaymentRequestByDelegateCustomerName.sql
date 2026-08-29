 
CREATE proc [dbo].[GetAllCustomersPaymentRequestByDelegateCustomerName]
@DelegateID int = NULL,
@CustomerName nvarchar(255)
as
select * from View_CustomersPaymentsRequest
where DelegateID=@DelegateID and CustomerName like N'%'+@CustomerName+N'%' order by CustomersPaymentsRequestID

