create   view [dbo].[View_SelectItemsSales]
AS
WITH CustomerSalesData AS (SELECT        CustomerSaleID, CustomerID, DelegateID, AccountZero, DateCreate
                                                                  FROM            dbo.CustomersSales), ItemsData AS
    (SELECT        ItemID, StoreID, ItemName, ISNULL(ItemPrice * 1448, 0) AS ItemPriceDenar, ISNULL(ItemCost * 1448, 0) AS ItemCostDenar, ISNULL(AmountDay * 1448, 0) AS AmountDayDenar
      FROM            dbo.Items), StoresData AS
    (SELECT        StoreID, StoreName
      FROM            dbo.Stores)
    SELECT        SIS.SelectItemsSaleID, SIS.UserID, SIS.CustomerSaleID, SIS.ItemID, SIS.Quantity, SIS.AsyncState, SIS.AsyncID, CSD.CustomerID, CSD.DelegateID, CSD.AccountZero, U.UserName, ID.StoreID, SD.StoreName, ID.ItemName, 
                              ID.ItemPriceDenar, ID.ItemCostDenar, ID.AmountDayDenar, ID.ItemPriceDenar * SIS.Quantity AS ItemPriceTotalDenar, ID.ItemCostDenar * SIS.Quantity AS ItemCostTotalDenar, 
                              ID.AmountDayDenar * SIS.Quantity AS AmountDayTotalDenar, CSD.DateCreate
     FROM            dbo.SelectItemsSales AS SIS LEFT OUTER JOIN
                              CustomerSalesData AS CSD ON SIS.CustomerSaleID = CSD.CustomerSaleID LEFT OUTER JOIN
                              ItemsData AS ID ON SIS.ItemID = ID.ItemID LEFT OUTER JOIN
                              StoresData AS SD ON ID.StoreID = SD.StoreID LEFT OUTER JOIN
                              dbo.Users AS U ON SIS.UserID = U.UserID

