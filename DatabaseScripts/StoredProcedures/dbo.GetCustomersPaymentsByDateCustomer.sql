CREATE proc [dbo].[GetCustomersPaymentsByDateCustomer]
@CustomerID int = NULL,
@FromDate datetime,
@ToDate datetime
as
select * from View_CustomersPayments
where CustomerID=@CustomerID and CONVERT(date, PaymentDate)>=@FromDate and CONVERT(date, PaymentDate)<=@ToDate order by CustomerPaymentID

