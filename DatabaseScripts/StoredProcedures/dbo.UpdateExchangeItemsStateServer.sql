

CREATE proc [dbo].[UpdateExchangeItemsStateServer]
@ExchangeItemID int =null,
@ExchangeItemsState bit = null
as
update ExchangeItems set ExchangeItemsState=@ExchangeItemsState where ExchangeItemID=@ExchangeItemID

