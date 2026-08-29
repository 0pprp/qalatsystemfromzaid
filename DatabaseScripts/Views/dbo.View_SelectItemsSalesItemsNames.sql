create   view [dbo].[View_SelectItemsSalesItemsNames]
as
SELECT        dbo.SelectItemsSales.Quantity, dbo.Customers.CustomerID, dbo.Items.ItemName, dbo.CustomersSales.CustomerSaleID
FROM            dbo.SelectItemsSales INNER JOIN
                         dbo.CustomersSales ON dbo.SelectItemsSales.CustomerSaleID = dbo.CustomersSales.CustomerSaleID INNER JOIN
                         dbo.Customers ON dbo.CustomersSales.CustomerID = dbo.Customers.CustomerID INNER JOIN
                         dbo.Items ON dbo.SelectItemsSales.ItemID = dbo.Items.ItemID

