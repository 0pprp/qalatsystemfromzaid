CREATE proc [dbo].[GetCustomerNoViewByID]
@CustomerID int = NULL
as
select * from Customers where CustomerID=@CustomerID

