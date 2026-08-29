CREATE proc [dbo].[AsyncExchangeItems]
as
select * from ExchangeItems where AsyncState='false'

