CREATE proc [dbo].[AsyncExchangeItemsDebts]
as
select * from ExchangeItemsDebts where AsyncState='false'

