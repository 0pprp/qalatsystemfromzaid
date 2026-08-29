CREATE proc [dbo].[Items_GetByItemBuy]
@StoreID int
as
select ItemID,ItemName,ItemCostDenar,ItemPriceDenar from View_Items where StoreID=@StoreID and ItemState=1

