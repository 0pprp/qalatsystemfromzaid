CREATE proc [dbo].[GetExchangeItemsByID]
@ExchangeItemID int = NULL
as
select * from View_ExchangeItems where ExchangeItemID=@ExchangeItemID

