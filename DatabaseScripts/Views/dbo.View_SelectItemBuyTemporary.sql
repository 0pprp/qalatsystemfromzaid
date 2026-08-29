create   view [dbo].[View_SelectItemBuyTemporary]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), ItemData AS
    (SELECT        ItemID, ItemName, ISNULL(ItemPriceDenar, 0) AS ItemPriceDenar, ISNULL(ItemCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
      FROM            dbo.View_Items), ItemCalculations AS
    (SELECT        SIT.ItemID, SIT.Quantity, ISNULL(I.ItemPriceDenar * SIT.Quantity, 0) AS ItemPriceTotalDenar, ISNULL(I.ItemCostDenar * SIT.Quantity, 0) AS ItemCostTotalDenar, ISNULL(I.AmountDayDenar * SIT.Quantity, 0) 
                                AS AmountDayTotalDenar
      FROM            dbo.SelectItemBuyTemporary AS SIT LEFT OUTER JOIN
                                ItemData AS I ON SIT.ItemID = I.ItemID)
    SELECT        SIT.SelectItemBuyTemporaryID, SIT.ItemID, SIT.Quantity, SIT.UserID, SIT.AsyncState, SIT.AsyncID, UD.UserName, ID.ItemName, ID.ItemPriceDenar, ID.ItemCostDenar, ID.AmountDayDenar, IC.ItemPriceTotalDenar, 
                              IC.ItemCostTotalDenar, IC.AmountDayTotalDenar
     FROM            dbo.SelectItemBuyTemporary AS SIT LEFT OUTER JOIN
                              UserData AS UD ON SIT.UserID = UD.UserID LEFT OUTER JOIN
                              ItemData AS ID ON SIT.ItemID = ID.ItemID LEFT OUTER JOIN
                              ItemCalculations AS IC ON SIT.ItemID = IC.ItemID

