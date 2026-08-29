create proc [dbo].[Customers_InfoSimple]
@CustomerID int
as
select * from View_CustomersInfoSimple where CustomerID=@CustomerID

