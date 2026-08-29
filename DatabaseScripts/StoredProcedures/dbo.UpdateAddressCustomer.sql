CREATE proc [dbo].[UpdateAddressCustomer]
@CustomerID int = NULL,
@Address nvarchar(255)
as
update Customers set Address=@Address where CustomerID=@CustomerID

