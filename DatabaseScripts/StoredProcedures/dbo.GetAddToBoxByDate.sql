CREATE proc [dbo].[GetAddToBoxByDate]
@FromDate datetime,
@ToDate datetime
as
SELECT     * from View_AddToBox

where CONVERT(date, DateCreate)>=@FromDate and CONVERT(date, DateCreate)<=@ToDate

