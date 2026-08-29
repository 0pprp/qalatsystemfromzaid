CREATE proc [dbo].[GetCustomersPaymentsRequestsByDelegateID]
@DelegateID int
as
select * from CustomersPaymentsRequest where DelegateID=@DelegateID

