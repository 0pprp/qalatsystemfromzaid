
CREATE proc [dbo].[GetAmountDayZeroByDelegate]
@FromDate datetime ,
@ToDate datetime ,
@DelegateId int
as
select * from CustomerZeroRemainingByDate where DelegateId=@DelegateId and CONVERT(date, LastPaymentDate)>=@FromDate and CONVERT(date, LastPaymentDate)<=@ToDate and AmountRemaining=0

