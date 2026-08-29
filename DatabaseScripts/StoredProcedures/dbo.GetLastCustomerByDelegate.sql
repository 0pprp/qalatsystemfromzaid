CREATE proc [dbo].[GetLastCustomerByDelegate]
@DelegateID int = NULL
as
select top 1 * from View_CustomersDelegate where DelegateID=@DelegateID order by CustomerID desc

