CREATE proc [dbo].[GetSupplierAccountByDateByType]
@SupplierID int = NULL,
@FromDate datetime,
@ToDate datetime,
@AccountType nvarchar(255)
as
select * from View_SuppliersAccounts
where 
SupplierID=@SupplierID 
and
CONVERT(date, AccountsDate)>=@FromDate
and 
CONVERT(date, AccountsDate)<=@ToDate
and 
AccountType=@AccountType

