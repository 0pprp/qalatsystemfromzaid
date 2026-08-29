CREATE proc [dbo].[GetSuppliersAccountByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_SuppliersAccounts
where 
CONVERT(date, AccountsDate)>=@FromDate
and 
CONVERT(date, AccountsDate)<=@ToDate

