CREATE proc [dbo].[GetTransferStoresByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_TransferStores
where 
CONVERT(date, TransferStoreDate)>=@FromDate
and 
CONVERT(date, TransferStoreDate)<=@ToDate

