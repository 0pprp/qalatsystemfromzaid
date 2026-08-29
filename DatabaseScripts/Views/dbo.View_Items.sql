CREATE view [dbo].[View_Items]
as
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), StoreData AS
    (SELECT        StoreID, StoreName
      FROM            dbo.Stores), SalesData AS
    (SELECT        ItemID, ISNULL(SUM(Quantity), 0) AS TotalQuantitySales
      FROM            dbo.View_SelectItemsSales
      GROUP BY ItemID), BuysData AS
    (SELECT        ItemID, ISNULL(SUM(Quantity), 0) AS TotalQuantityBuys
      FROM            dbo.View_BuysItems
      GROUP BY ItemID)
    SELECT        dbo.Items.ItemID, dbo.Items.StoreID, dbo.Items.UserID, dbo.Items.ItemName, dbo.Items.ItemPrice, dbo.Items.ItemCost, dbo.Items.Quantity, dbo.Items.ItemImage, dbo.Items.Notes, dbo.Items.NotificationNumber, 
                              dbo.Items.AmountDay, dbo.Items.NumberOfSales, dbo.Items.AsyncState, dbo.Items.AsyncID, dbo.Items.Link, dbo.Items.ItemState, UserData_1.UserName, StoreData_1.StoreName, ISNULL(dbo.Items.ItemPrice * 1448, 0) AS ItemPriceDenar, ISNULL(dbo.Items.ItemCost * 1448, 0) AS ItemCostDenar, ISNULL(dbo.Items.AmountDay * 1448, 0) AS AmountDayDenar, ISNULL(SalesData_1.TotalQuantitySales, 0) 
                              AS NumberOfItemsSales, ISNULL(BuysData_1.TotalQuantityBuys, 0) AS NumberOfItemsBuys, ISNULL(dbo.Items.ItemPrice * 1448, 0) * dbo.Items.Quantity AS PriceTotalItem, 
                              ISNULL(dbo.Items.ItemCost * 1448, 0) * dbo.Items.Quantity AS CostTotalItem
     FROM            dbo.Items LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.Items.UserID = UserData_1.UserID LEFT OUTER JOIN
                              StoreData AS StoreData_1 ON dbo.Items.StoreID = StoreData_1.StoreID LEFT OUTER JOIN
                              SalesData AS SalesData_1 ON dbo.Items.ItemID = SalesData_1.ItemID LEFT OUTER JOIN
                              BuysData AS BuysData_1 ON dbo.Items.ItemID = BuysData_1.ItemID


