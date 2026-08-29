CREATE proc [dbo].[AsyncWithdrawalStores]
as
select * from WithdrawalStores where AsyncState='false'

