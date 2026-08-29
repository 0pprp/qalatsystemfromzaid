CREATE proc [dbo].[GetExchangeItemsServerData]
as
select ExchangeItemID,AsyncID,ExchangeItemsState from ExchangeItems

