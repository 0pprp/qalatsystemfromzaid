CREATE proc [dbo].[GetCustomerByID]
@CustomerID int = NULL
as
select * from View_CustomersDelegate where CustomerID=@CustomerID

