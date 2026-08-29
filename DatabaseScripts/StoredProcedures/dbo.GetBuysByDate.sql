CREATE proc [dbo].[GetBuysByDate]
@FromDate datetime,
@ToDate  datetime
as
SELECT   * from View_Buys

where  CONVERT(date, DateCreate)>=@FromDate and CONVERT(date, DateCreate)<=@ToDate

