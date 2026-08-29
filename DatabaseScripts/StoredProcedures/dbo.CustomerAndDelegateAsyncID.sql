CREATE proc [dbo].[CustomerAndDelegateAsyncID]
as
select
CustomerID,
DelegateID,
(AsyncID)as CustomerAsyncID,
(select AsyncID from Delegates where DelegateID=Customers.DelegateID)as DelegateAsyncID 
from Customers

