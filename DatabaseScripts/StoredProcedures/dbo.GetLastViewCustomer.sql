CREATE proc [dbo].[GetLastViewCustomer]
@DelegateID int = NULL
as
select top 1 * from View_CustomersDelegate where DelegateID=@DelegateID order by CustomerID desc

