CREATE proc [dbo].[GetExchangeItemsDebtsByName]
@ExchangeItemName nvarchar(255)
as
select * from View_ExchangeItemsDebts
where 
ExchangeItemName like N'%'+@ExchangeItemName+N'%' 

