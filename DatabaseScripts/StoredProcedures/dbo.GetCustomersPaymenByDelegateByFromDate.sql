CREATE proc [dbo].[GetCustomersPaymenByDelegateByFromDate]
@Date datetime,
@DelegateID int
as
select * from View_CustomersPayments where DelegateID=@DelegateID and CONVERT(date, PaymentDate)=@Date   order by CustomerPaymentID

