CREATE proc [dbo].[UpdateIsLegalCustomer]
@CustomerID int = NULL,
@IsLegal bit
as
update Customers set IsLegal=@IsLegal where CustomerID=@CustomerID

