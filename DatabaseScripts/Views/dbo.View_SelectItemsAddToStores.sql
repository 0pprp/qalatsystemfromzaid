create   view [dbo].[View_SelectItemsAddToStores]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), ItemData AS
    (SELECT        ItemID, ItemName
      FROM            dbo.Items), ItemDetails AS
    (SELECT        ItemID, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
      FROM            dbo.View_Items), AddToStoreData AS
    (SELECT        AddToStoreID, DateAddToStore
      FROM            dbo.AddToStores)
    SELECT        SIS.SelectItemAddToStoreID, SIS.AddToStoreID, SIS.UserID, SIS.ItemID, SIS.Quantity, SIS.AsyncState, SIS.AsyncID, UD.UserName, ID.ItemName, IDT.ItemPriceDenar, IDT.ItemCostDenar, IDT.AmountDayDenar, 
                              IDT.ItemPriceDenar * SIS.Quantity AS ItemPriceTotalDenar, IDT.ItemCostDenar * SIS.Quantity AS ItemCostTotalDenar, IDT.AmountDayDenar * SIS.Quantity AS AmountDayTotalDenar, ATS.DateAddToStore AS DateCreate
     FROM            dbo.SelectItemsAddToStores AS SIS LEFT OUTER JOIN
                              UserData AS UD ON SIS.UserID = UD.UserID LEFT OUTER JOIN
                              ItemData AS ID ON SIS.ItemID = ID.ItemID LEFT OUTER JOIN
                              ItemDetails AS IDT ON SIS.ItemID = IDT.ItemID LEFT OUTER JOIN
                              AddToStoreData AS ATS ON SIS.AddToStoreID = ATS.AddToStoreID

