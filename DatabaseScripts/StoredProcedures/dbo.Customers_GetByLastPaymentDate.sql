create proc Customers_GetByLastPaymentDate  
@FromDate datetime,
@ToDate datetime
as
select * from View_CustomersDelegate where AmountRemaining>0 and IsLegal=0 and CONVERT(date,LastPaymentDate)>=CONVERT(date,@FromDate) and CONVERT(date,LastPaymentDate)<=CONVERT(date,@ToDate) order by LastPaymentDate
