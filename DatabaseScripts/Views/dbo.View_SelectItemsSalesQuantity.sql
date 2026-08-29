create   view [dbo].[View_SelectItemsSalesQuantity]
AS
WITH ItemsData AS (SELECT        ItemID, ISNULL(ItemPrice * 1448, 0) AS ItemPriceDenar, ISNULL(AmountDay * 1448, 0) AS AmountDayDenar
                                             FROM            dbo.Items)
    SELECT        SIS.SelectItemsSaleID, SIS.CustomerSaleID, SIS.ItemID, SIS.Quantity, ID.ItemPriceDenar, ID.AmountDayDenar
     FROM            dbo.SelectItemsSales AS SIS LEFT OUTER JOIN
                              ItemsData AS ID ON SIS.ItemID = ID.ItemID

