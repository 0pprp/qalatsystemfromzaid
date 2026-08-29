 
CREATE proc [dbo].[GetAllCustomersPaymentRequestByDelegate]
@DelegateID int = NULL
as
select * from View_CustomersPaymentsRequest
where DelegateID=@DelegateID order by CustomersPaymentsRequestID

