CREATE proc [dbo].[GetCustomersPaymentsByCustomer]
@CustomerID int = NULL
as
select * from View_CustomersPayments
where CustomerID=@CustomerID order by CustomerPaymentID

