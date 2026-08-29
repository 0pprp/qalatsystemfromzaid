CREATE proc [dbo].[GetLastCustomer]
@DelegateID int = NULL
as
select top 1 * from Customers  where DelegateID=@DelegateID order by CustomerID desc

