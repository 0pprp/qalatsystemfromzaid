
CREATE proc [dbo].[AsyncStateUpdateExchangeItems]
@ExchangeItemID int = NULL
as
update ExchangeItems set AsyncState='true' where ExchangeItemID=@ExchangeItemID

