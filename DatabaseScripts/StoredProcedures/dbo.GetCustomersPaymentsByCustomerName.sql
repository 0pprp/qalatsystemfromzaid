CREATE proc [dbo].[GetCustomersPaymentsByCustomerName]
@CustomerName nvarchar(255)
as
select * from View_CustomersPayments
where CustomerName like N'%'+@CustomerName+N'%' order by CustomerPaymentID

