CREATE proc [dbo].[GetCustomersPaymentsByDateDelegate]
@DelegateID int = NULL,
@FromDate datetime,
@ToDate datetime
as
select * from View_CustomersPayments
where DelegateID=@DelegateID and CONVERT(date, PaymentDate)>=@FromDate and CONVERT(date, PaymentDate)<=@ToDate order by CustomerPaymentID

