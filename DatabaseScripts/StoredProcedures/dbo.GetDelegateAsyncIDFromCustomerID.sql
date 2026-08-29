CREATE proc [dbo].[GetDelegateAsyncIDFromCustomerID]
@CustomerID int = NULL
as
select 
(select AsyncID from Delegates where DelegateID=Customers.DelegateID)as DelegateAsyncID 
from Customers where CustomerID=@CustomerID

