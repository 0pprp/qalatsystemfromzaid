CREATE proc [dbo].[GetCustomersPaymenByFromDate]
@Date datetime
as
select * from View_CustomersPayments where CONVERT(date, PaymentDate)=@Date   order by CustomerPaymentID

