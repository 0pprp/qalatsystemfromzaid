create   view [dbo].[View_BuysItems]
AS
SELECT        dbo.BuysItems.BuyItemID, dbo.BuysItems.UserID, dbo.BuysItems.BuyID, dbo.BuysItems.ItemID, dbo.BuysItems.Quantity, dbo.BuysItems.AsyncState, dbo.BuysItems.AsyncID, dbo.Users.UserName AS UserName, 
                         dbo.Items.StoreID AS StoreID, dbo.Stores.StoreName AS StoreName, dbo.Items.ItemName AS ItemName, ISNULL(dbo.Items.ItemPrice * 1448, 0) AS ItemPriceDenar, ISNULL(dbo.Items.ItemCost * 1448, 0) AS ItemCostDenar, 
                         ISNULL(dbo.Items.AmountDay * 1448, 0) AS AmountDayDenar, ISNULL(dbo.Items.ItemPrice * 1448 * dbo.BuysItems.Quantity, 0) AS ItemTotalPriceDenar, ISNULL(dbo.Items.ItemCost * 1448 * dbo.BuysItems.Quantity, 0) 
                         AS ItemTotalCostDenar, ISNULL(dbo.Items.AmountDay * 1448 * dbo.BuysItems.Quantity, 0) AS AmountTotalDayDenar, dbo.Buys.DateCreate AS DateCreate
FROM            dbo.BuysItems LEFT OUTER JOIN
                         dbo.Users ON dbo.BuysItems.UserID = dbo.Users.UserID LEFT OUTER JOIN
                         dbo.Items ON dbo.BuysItems.ItemID = dbo.Items.ItemID LEFT OUTER JOIN
                         dbo.Stores ON dbo.Items.StoreID = dbo.Stores.StoreID LEFT OUTER JOIN
                         dbo.Buys ON dbo.BuysItems.BuyID = dbo.Buys.BuyID

