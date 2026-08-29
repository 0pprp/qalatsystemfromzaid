CREATE proc [dbo].[GetCustomersPaymentsByDelegate]
@DelegateID int = NULL
as
select * from View_CustomersPayments
where DelegateID=@DelegateID order by CustomerPaymentID

