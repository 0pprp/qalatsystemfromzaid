CREATE proc [dbo].[GetWithdrawalStoresByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_WithdrawalStores
where CONVERT(date, WithdrawalStoresDate)>=@FromDate
and CONVERT(date, WithdrawalStoresDate)<=@ToDate

