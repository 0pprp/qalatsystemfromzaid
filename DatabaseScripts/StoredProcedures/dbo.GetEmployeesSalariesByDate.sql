CREATE proc [dbo].[GetEmployeesSalariesByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_EmployeesSalaries
where CONVERT(date, SalaryDate)>=@FromDate
and 
CONVERT(date, SalaryDate)<=@ToDate

