CREATE proc [dbo].[GetCustomerAsyncID]
@CustomerID  int = NULL
as
select top 1 AsyncID from Customers where CustomerID=@CustomerID

