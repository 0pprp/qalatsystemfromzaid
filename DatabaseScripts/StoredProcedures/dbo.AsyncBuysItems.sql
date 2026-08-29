CREATE proc [dbo].[AsyncBuysItems]
as
select * from BuysItems where AsyncState='false'

