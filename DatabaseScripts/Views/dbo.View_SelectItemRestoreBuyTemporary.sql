create   view [dbo].[View_SelectItemRestoreBuyTemporary]
AS
SELECT        SelectItemRestoreBuyTemporaryID, ItemID, Quantity, UserID, AsyncState, AsyncID,
                             (SELECT        UserName
                               FROM            dbo.Users
                               WHERE        (UserID = dbo.SelectItemRestoreBuyTemporary.UserID)) AS UserName,
                             (SELECT        ItemName
                               FROM            dbo.Items
                               WHERE        (ItemID = dbo.SelectItemRestoreBuyTemporary.ItemID)) AS ItemName,
                             (SELECT        ISNULL(ItemPriceDenar, 0) AS Expr1
                               FROM            dbo.View_Items
                               WHERE        (ItemID = dbo.SelectItemRestoreBuyTemporary.ItemID)) AS ItemPriceDenar,
                             (SELECT        ISNULL(ItemCostDenar, 0) AS Expr1
                               FROM            dbo.View_Items AS View_Items_5
                               WHERE        (ItemID = dbo.SelectItemRestoreBuyTemporary.ItemID)) AS ItemCostDenar,
                             (SELECT        ISNULL(AmountDayDenar, 0) AS Expr1
                               FROM            dbo.View_Items AS View_Items_4
                               WHERE        (ItemID = dbo.SelectItemRestoreBuyTemporary.ItemID)) AS AmountDayDenar,
                             (SELECT        ISNULL(ItemPriceDenar * dbo.SelectItemRestoreBuyTemporary.Quantity, 0) AS Expr1
                               FROM            dbo.View_Items AS View_Items_3
                               WHERE        (ItemID = dbo.SelectItemRestoreBuyTemporary.ItemID)) AS ItemPriceTotalDenar,
                             (SELECT        ISNULL(ItemCostDenar * dbo.SelectItemRestoreBuyTemporary.Quantity, 0) AS Expr1
                               FROM            dbo.View_Items AS View_Items_2
                               WHERE        (ItemID = dbo.SelectItemRestoreBuyTemporary.ItemID)) AS ItemCostDenarDenar,
                             (SELECT        ISNULL(AmountDayDenar * dbo.SelectItemRestoreBuyTemporary.Quantity, 0) AS Expr1
                               FROM            dbo.View_Items AS View_Items_1
                               WHERE        (ItemID = dbo.SelectItemRestoreBuyTemporary.ItemID)) AS AmountDayTotalDenar
FROM            dbo.SelectItemRestoreBuyTemporary

