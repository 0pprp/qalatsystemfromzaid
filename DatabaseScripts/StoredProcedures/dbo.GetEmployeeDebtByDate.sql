CREATE proc [dbo].[GetEmployeeDebtByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_EmployeeDebts
where CONVERT(date, DateDebt)>=@FromDate
and 
CONVERT(date, DateDebt)<=@ToDate

