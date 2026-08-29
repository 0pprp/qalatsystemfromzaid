CREATE proc [dbo].[GetBuyAsyncID]
@BuyID int = NULL
as
select   AsyncID from Buys where BuyID=@BuyID

