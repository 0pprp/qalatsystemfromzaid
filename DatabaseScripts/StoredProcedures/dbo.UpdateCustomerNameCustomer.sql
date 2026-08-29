CREATE proc [dbo].[UpdateCustomerNameCustomer]
@CustomerID int = NULL,
@CustomerName nvarchar(255)
as
update Customers set CustomerName=@CustomerName where CustomerID=@CustomerID

