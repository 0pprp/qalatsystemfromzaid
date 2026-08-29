CREATE proc [dbo].[GetSupplierAccountByDate]
@SupplierID int = NULL,
@FromDate datetime,
@ToDate datetime
as
select * from View_SuppliersAccounts
where 
SupplierID=@SupplierID 
and
CONVERT(date, AccountsDate)>=@FromDate
and 
CONVERT(date, AccountsDate)<=@ToDate

