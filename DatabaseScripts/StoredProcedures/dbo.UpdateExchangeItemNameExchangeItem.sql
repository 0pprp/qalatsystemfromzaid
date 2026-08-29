CREATE proc [dbo].[UpdateExchangeItemNameExchangeItem]
@ExchangeItemID int = NULL,
@ExchangeItemName nvarchar(255)
as
update ExchangeItems set ExchangeItemName=@ExchangeItemName where ExchangeItemID=@ExchangeItemID

