CREATE proc [dbo].[GetCustomersPaymentsBySingleDateCustomer]
@CustomerID int = NULL,
@DateCreate datetime
as
select * from View_CustomersPayments
where CustomerID=@CustomerID and CONVERT(date, PaymentDate)=@DateCreate order by CustomerPaymentID 

