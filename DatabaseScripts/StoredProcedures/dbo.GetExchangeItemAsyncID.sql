CREATE proc [dbo].[GetExchangeItemAsyncID]
@ExchangeItemID int = NULL
as
select AsyncID from ExchangeItems where ExchangeItemID=@ExchangeItemID

