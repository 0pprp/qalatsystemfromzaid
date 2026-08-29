CREATE proc [dbo].[Customers_Move]
@CustomerID int,
@DelegateID int,
@UserUpdateID int
as
update Customers set DelegateID=@DelegateID where CustomerID=@CustomerID
update CustomersSales set DelegateID=@DelegateID where CustomerID=@CustomerID
update CustomersPayments set DelegateID=@DelegateID where CustomerID=@CustomerID
select * from View_CustomersDelegate where CustomerID=@CustomerID

