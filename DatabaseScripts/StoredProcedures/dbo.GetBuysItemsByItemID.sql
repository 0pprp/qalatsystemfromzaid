CREATE proc [dbo].[GetBuysItemsByItemID]
@ItemID int = NULL
as
 SELECT   *  FROM           View_BuysItems

where  ItemID=@ItemID

