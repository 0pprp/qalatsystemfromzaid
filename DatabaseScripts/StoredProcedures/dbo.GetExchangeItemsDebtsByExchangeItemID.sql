CREATE proc [dbo].[GetExchangeItemsDebtsByExchangeItemID]
@ExchangeItemID int = NULL
as
select * from View_ExchangeItemsDebts where ExchangeItemID=@ExchangeItemID

