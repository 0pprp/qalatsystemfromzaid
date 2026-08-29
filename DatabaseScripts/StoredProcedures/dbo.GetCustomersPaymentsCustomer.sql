CREATE proc [dbo].[GetCustomersPaymentsCustomer]
@CustomerID int
as
select * from View_CustomersPayments where CustomerID=@CustomerID  

