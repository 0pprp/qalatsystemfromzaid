
CREATE proc [dbo].[UpdateCustomerStateServer]
@CustomerID int =null,
@CustomerState bit = null
as
update Customers set CustomerState=@CustomerState where CustomerID=@CustomerID

