CREATE proc [dbo].[GetCustomersPaymenDelegate]
@DelegateID int = NULL
as
select * from View_CustomersPayments
where DelegateID=@DelegateID  

