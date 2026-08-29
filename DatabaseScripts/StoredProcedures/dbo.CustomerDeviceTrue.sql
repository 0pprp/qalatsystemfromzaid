CREATE proc [dbo].[CustomerDeviceTrue]
@CustomerID int = NULL
as
update CustomersSales set AccountZero='true' where CustomerID=@CustomerID  
update CustomersPayments set AccountZero='true' where CustomerID=@CustomerID  

