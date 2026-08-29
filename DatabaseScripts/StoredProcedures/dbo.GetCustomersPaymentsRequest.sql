CREATE proc [dbo].[GetCustomersPaymentsRequest]
as
select *,
(select CustomerName from Customers where CustomerID=CustomersPaymentsRequest.CustomerID)as CustomerName,
(select AsyncID from Customers where CustomerID=CustomersPaymentsRequest.CustomerID)as CustomerAsyncID,
(select DelegateName from Delegates where DelegateID=CustomersPaymentsRequest.DelegateID)as DelegateName ,
(select AsyncID from Delegates where DelegateID=CustomersPaymentsRequest.DelegateID)as DelegateAsyncID
from CustomersPaymentsRequest where DelegateID>0 and CustomerID>0 and Amount>0 

