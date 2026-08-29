CREATE proc [dbo].[AsyncWithdrawalFromBox]
as
select * from WithdrawalFromBox where AsyncState='false'

