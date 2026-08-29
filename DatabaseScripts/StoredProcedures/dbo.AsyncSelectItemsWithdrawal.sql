CREATE proc [dbo].[AsyncSelectItemsWithdrawal]
as
select * from SelectItemsWithdrawal where AsyncState='false'

