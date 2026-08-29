CREATE proc [dbo].[GetCustomersPaymentsByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_CustomersPayments
where CONVERT(date, PaymentDate)>=@FromDate and CONVERT(date, PaymentDate)<=@ToDate order by CustomerPaymentID

