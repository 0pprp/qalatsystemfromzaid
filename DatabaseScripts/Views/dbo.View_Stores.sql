create   view [dbo].[View_Stores]
AS
SELECT        S.StoreID, S.UserID, S.StoreName, S.StorePlace, S.Notes, S.CityID, S.AsyncState, S.AsyncID, S.State, U.UserName, C.CityName, ISNULL(VCS.TotalSales, 0) AS TotalPrice, ISNULL(VCS.TotalCost, 0) AS TotalCost, 
                         ISNULL(VB.TotalSpent, 0) AS AmountExchange, ISNULL(VI.SalesCost, 0) AS CostSalesItemsCurrent, ISNULL(VI.BuyCost, 0) AS CostBuyItemsCurrent, ISNULL(I.ItemCount, 0) AS NumberOfTypes, ISNULL(I.TotalQuantity, 0) 
                         AS NumberOfItems, ISNULL(VBI.BuyQuantity, 0) AS NumberOfItemBuy, ISNULL(VSI.SalesQuantity, 0) AS NumberOfItemsSales, ISNULL(VBI.TotalCost, 0) AS AmountItemBuy
FROM            dbo.Stores AS S LEFT OUTER JOIN
                         dbo.Users AS U ON S.UserID = U.UserID LEFT OUTER JOIN
                         dbo.Cities AS C ON S.CityID = C.CityID LEFT OUTER JOIN
                             (SELECT        StoreID, SUM(AmountTotalSalesDenar) AS TotalSales, SUM(AmountTotalCostDenar) AS TotalCost
                               FROM            dbo.View_CustomersSales
                               GROUP BY StoreID) AS VCS ON S.StoreID = VCS.StoreID LEFT OUTER JOIN
                             (SELECT        StoreID, SUM(AmountSpentDenar) AS TotalSpent
                               FROM            dbo.View_Buys
                               GROUP BY StoreID) AS VB ON S.StoreID = VB.StoreID LEFT OUTER JOIN
                             (SELECT        StoreID, SUM(PriceTotalItem) AS SalesCost, SUM(CostTotalItem) AS BuyCost
                               FROM            dbo.View_Items
                               GROUP BY StoreID) AS VI ON S.StoreID = VI.StoreID LEFT OUTER JOIN
                             (SELECT        StoreID, COUNT(*) AS ItemCount, SUM(Quantity) AS TotalQuantity
                               FROM            dbo.Items
                               GROUP BY StoreID) AS I ON S.StoreID = I.StoreID LEFT OUTER JOIN
                             (SELECT        StoreID, SUM(Quantity) AS BuyQuantity, SUM(ItemCostDenar) AS TotalCost
                               FROM            dbo.View_BuysItems
                               GROUP BY StoreID) AS VBI ON S.StoreID = VBI.StoreID LEFT OUTER JOIN
                             (SELECT        StoreID, SUM(Quantity) AS SalesQuantity
                               FROM            dbo.View_SelectItemsSales
                               GROUP BY StoreID) AS VSI ON S.StoreID = VSI.StoreID

