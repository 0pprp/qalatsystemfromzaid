

CREATE view [dbo].[View_ItemsData]
as
SELECT        ItemState, ItemID, StoreID, ItemName, Quantity, ISNULL(ItemPrice * 1448, 0) AS ItemPriceDenar, ISNULL(ItemCost * 1448, 0) AS ItemCostDenar, ISNULL(AmountDay * 1448, 0)
                         AS AmountDayDenar
FROM            dbo.Items

