
CREATE proc [dbo].[AsyncStateUpdateExchangeItemsDebts]
@ExchangeItemDebtID int = NULL
as
update ExchangeItemsDebts set AsyncState='true' where ExchangeItemDebtID=@ExchangeItemDebtID

