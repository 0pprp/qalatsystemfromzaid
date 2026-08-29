
CREATE proc [dbo].[AsyncStateUpdateCustomers]
@CustomerID int = NULL
as
update Customers set AsyncState='true' where CustomerID=@CustomerID

