
CREATE proc [dbo].[SumAmountDayByDelegate]
@DelegateID int
as
select * from [dbo].[ViewAmountDayAndRemaining] where DelegateID=@DelegateID

