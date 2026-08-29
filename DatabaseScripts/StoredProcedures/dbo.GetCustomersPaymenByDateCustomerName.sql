CREATE proc [dbo].[GetCustomersPaymenByDateCustomerName]
@FromDate datetime,
@ToDate datetime,
@CustomerName nvarchar(255)
as
select * from View_CustomersPayments
where  CONVERT(date, PaymentDate)>=@FromDate and CONVERT(date, PaymentDate)<=@ToDate and
CustomerName like  N'%'+@CustomerName+N'%'  order by CustomerPaymentID

