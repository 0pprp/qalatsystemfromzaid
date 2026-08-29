CREATE proc [dbo].[DeleteExchangeItemsDebtsAsyncID]
@ExchangeItemDebtID int = NULL
as
insert into DeleteData (ExchangeItemsDebtsAsyncID) values ((select AsyncID from ExchangeItemsDebts where ExchangeItemDebtID=@ExchangeItemDebtID))

