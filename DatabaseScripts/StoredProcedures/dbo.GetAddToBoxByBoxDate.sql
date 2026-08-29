CREATE proc [dbo].[GetAddToBoxByBoxDate]
@BoxID int = NULL,
@FromDate datetime,
@ToDate datetime
as
SELECT     * from View_AddToBox

where BoxID=@BoxID and CONVERT(date, DateCreate)>=@FromDate and CONVERT(date, DateCreate)<=@ToDate

