CREATE proc [dbo].[GetWithdrawalFromBoxByDate]
@FromDate datetime,
@ToDate datetime
as
select * from View_WithdrawalFromBox
where CONVERT(date, DateCreate)>=@FromDate and
CONVERT(date, DateCreate)<=@ToDate

