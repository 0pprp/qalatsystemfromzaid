CREATE proc [dbo].[CustomerDeviceFalse]
@CustomerID int = NULL
as
update CustomersSales set AccountZero='false' where CustomerID=@CustomerID  
update CustomersPayments set AccountZero='false' where CustomerID=@CustomerID  

