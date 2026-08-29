CREATE proc [dbo].[GetWithdrawalFromBoxByBoxDate]
@BoxID int = NULL,
@FromDate datetime,
@ToDate datetime
as
select * from View_WithdrawalFromBox
where BoxID=@BoxID and CONVERT(date, DateCreate)>=@FromDate and
CONVERT(date, DateCreate)<=@ToDate

