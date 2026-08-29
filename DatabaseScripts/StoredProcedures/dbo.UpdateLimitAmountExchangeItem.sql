CREATE proc [dbo].[UpdateLimitAmountExchangeItem]
@ExchangeItemID int = NULL,
@LimitAmount float
as
update ExchangeItems set LimitAmount=@LimitAmount where ExchangeItemID=@ExchangeItemID

