CREATE proc [dbo].[GetExchangeItemsDebtByDate]
@FromDate datetime ,
@ToDate datetime 
as
select * from View_ExchangeItemsDebts
where  
CONVERT(date, DateDebt) >=@FromDate
and 
CONVERT(date, DateDebt) <=@ToDate

