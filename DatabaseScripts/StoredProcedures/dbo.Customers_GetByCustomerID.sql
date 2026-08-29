create proc [dbo].[Customers_GetByCustomerID]
@CustomerID int
as
select * from View_CustomersDelegate where CustomerID=@CustomerID

