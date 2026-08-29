CREATE proc [dbo].[GetTransferBoxsByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_TransferBoxs
where 
CONVERT(date, DateCreate)>=@FromDate
and 
CONVERT(date, DateCreate)<=@ToDate

