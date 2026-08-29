CREATE proc [dbo].[GetCustomerRemaining]
@CustomerID int = NULL
as
select * from View_CustomersRemaining
where CustomerID=@CustomerID

