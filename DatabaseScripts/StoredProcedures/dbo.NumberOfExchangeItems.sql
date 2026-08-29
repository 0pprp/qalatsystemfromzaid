 
CREATE proc [dbo].[NumberOfExchangeItems]
as
select count(*) as NumberOfExchangeItems from  ExchangeItems where ExchangeItemsState='true'


