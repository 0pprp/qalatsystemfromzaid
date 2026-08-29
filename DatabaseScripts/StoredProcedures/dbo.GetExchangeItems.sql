CREATE proc [dbo].[GetExchangeItems]
 
as
select * from View_ExchangeItems where ExchangeItemsState='true'

