CREATE proc [dbo].[GetCustomerByIDData]
@CustomerID int
as
select * from Customers where CustomerID=@CustomerID

