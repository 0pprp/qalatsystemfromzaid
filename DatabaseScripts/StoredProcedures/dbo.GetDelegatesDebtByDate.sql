CREATE proc [dbo].[GetDelegatesDebtByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_DelegatesDebts
where CONVERT(date, DateDebt)>=@FromDate
and 
CONVERT(date, DateDebt)<=@ToDate

