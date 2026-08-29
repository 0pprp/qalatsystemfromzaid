CREATE proc [dbo].[GetCustomersPaymentByDateDelegate]
@FromDate datetime ,
@ToDate datetime,
@DelegateID int = NULL
as
select * from View_CustomersPayments
where DelegateID=@DelegateID and CONVERT(date, PaymentDate)>=@FromDate and 
CONVERT(date, PaymentDate)<=@ToDate order by CustomerPaymentID

