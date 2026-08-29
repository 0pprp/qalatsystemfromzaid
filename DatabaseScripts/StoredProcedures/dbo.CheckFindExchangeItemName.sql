CREATE proc [dbo].[CheckFindExchangeItemName]
@ExchangeItemName nvarchar(255)
as
select * from ExchangeItems where ExchangeItemName=@ExchangeItemName

