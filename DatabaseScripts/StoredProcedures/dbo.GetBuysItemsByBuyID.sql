CREATE proc [dbo].[GetBuysItemsByBuyID]
@BuyID int = NULL
as
 SELECT   *  FROM           View_BuysItems

where  BuyID=@BuyID

