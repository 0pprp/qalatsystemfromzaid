CREATE proc [dbo].[GetAmountDayZero]
@FromDate datetime ,
@ToDate datetime 
as
select * from CustomerZeroRemainingByDate where CONVERT(date, LastPaymentDate)>=@FromDate and CONVERT(date, LastPaymentDate)<=@ToDate and AmountRemaining=0



