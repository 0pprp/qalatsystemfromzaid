create   view [dbo].[View_SelectItemsTransferStores]
AS
WITH ItemData AS (SELECT        ItemID, ItemName, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
                                          FROM            dbo.View_Items), TransferStoreData AS
    (SELECT        TransferStoreID, TransferStoreDate, State
      FROM            dbo.TransferStores), UserData AS
    (SELECT        UserID, UserName
      FROM            dbo.Users)
    SELECT        SIT.SelectItemTransferStoreID, SIT.TransferStoreID, SIT.UserID, SIT.ItemID, SIT.Quantity, SIT.AsyncState, SIT.AsyncID, UD.UserName, ID.ItemName, ID.ItemPriceDenar, ID.ItemCostDenar, ID.AmountDayDenar, 
                              ISNULL(ID.ItemPriceDenar * SIT.Quantity, 0) AS ItemPriceTotalDenar, ISNULL(ID.ItemCostDenar * SIT.Quantity, 0) AS ItemCostTotalDenar, ISNULL(ID.AmountDayDenar * SIT.Quantity, 0) AS AmountDayTotalDenar, 
                              TSD.TransferStoreDate AS DateCreate, TSD.State
     FROM            dbo.SelectItemsTransferStores AS SIT LEFT OUTER JOIN
                              UserData AS UD ON SIT.UserID = UD.UserID LEFT OUTER JOIN
                              ItemData AS ID ON SIT.ItemID = ID.ItemID LEFT OUTER JOIN
                              TransferStoreData AS TSD ON SIT.TransferStoreID = TSD.TransferStoreID

