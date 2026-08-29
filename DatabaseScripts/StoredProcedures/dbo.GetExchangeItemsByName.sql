CREATE proc [dbo].[GetExchangeItemsByName]
@ExchangeItemName nvarchar(255)
as
select * from View_ExchangeItems 
where  ExchangeItemsState='true' and
ExchangeItemName like N'%'+@ExchangeItemName+N'%' 

