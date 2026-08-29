CREATE proc [dbo].[GetExchangeItemsByCity]
@CityID int = NULL
as
select * from View_ExchangeItems where ExchangeItemsState='true' and CityID=@CityID

