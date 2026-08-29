CREATE proc [dbo].[ServerDataExchangeItemByAsyncID]
@AsyncID nvarchar(255) = null
as
select top 1 * from ExchangeItems where AsyncID=@AsyncID

