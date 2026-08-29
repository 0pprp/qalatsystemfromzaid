CREATE proc [dbo].[Items_GetByItemSale]
@StoreID int
as
select ItemID,ItemName,ItemPriceDenar,AmountDayDenar,Quantity from View_Items where StoreID=@StoreID and ItemState=1 and Quantity>0

