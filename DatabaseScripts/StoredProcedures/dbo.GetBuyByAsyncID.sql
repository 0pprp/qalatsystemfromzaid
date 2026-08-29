
create   proc [dbo].[GetBuyByAsyncID]
@AsyncID nvarchar(100)
as
select * from Buys where AsyncID=@AsyncID


