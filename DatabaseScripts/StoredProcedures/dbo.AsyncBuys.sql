CREATE proc [dbo].[AsyncBuys]
as
select * from Buys where AsyncState='false'

