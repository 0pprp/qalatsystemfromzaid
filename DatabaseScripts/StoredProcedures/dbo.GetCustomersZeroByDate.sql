CREATE proc [dbo].[GetCustomersZeroByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_CustomerWithLastPayment where AmountTotalSales=ReceiptsTotal and CONVERT(date, LastDatePayment)>=@FromDate and CONVERT(date, LastDatePayment)<=@ToDate  

