CREATE proc [dbo].[ServerDataBuyByAsyncID]
@AsyncID nvarchar(255) = NULL
as
select top 1 * from Buys where AsyncID=@AsyncID

