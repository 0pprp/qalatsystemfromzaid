CREATE proc [dbo].[GetCustomersPaymentByDateDelegateCustomerName]
@FromDate datetime,
@ToDate datetime,
@DelegateID int = NULL,
@CustomerName nvarchar(255)
as
select * from View_CustomersPayments
where DelegateID=@DelegateID and
CONVERT(date, PaymentDate)>=@FromDate and
CONVERT(date, PaymentDate)<=@ToDate and
CustomerName like  N'%'+@CustomerName+N'%'
order by CustomerPaymentID

