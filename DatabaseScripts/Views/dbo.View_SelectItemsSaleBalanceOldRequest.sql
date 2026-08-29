create   view [dbo].[View_SelectItemsSaleBalanceOldRequest]
AS
WITH BalanceData AS (SELECT        BalanceID, BalanceName
                                                  FROM            dbo.Balance), ViewBalanceData AS
    (SELECT        BalanceID, ISNULL(BalancePriceDenar, 0) AS ItemPriceDenar, ISNULL(BalanceCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
      FROM            dbo.View_Balance)
    SELECT        SISBOR.SelectItemsSaleBalanceOldRequestID, SISBOR.CustomerSaleBalanceRequestOldID, SISBOR.BalanceID, SISBOR.AsyncState, SISBOR.AsyncID, BD.BalanceName AS ItemName, VBD.ItemPriceDenar, 
                              VBD.ItemCostDenar, VBD.AmountDayDenar
     FROM            dbo.SelectItemsSaleBalanceOldRequest AS SISBOR LEFT OUTER JOIN
                              BalanceData AS BD ON SISBOR.BalanceID = BD.BalanceID LEFT OUTER JOIN
                              ViewBalanceData AS VBD ON SISBOR.BalanceID = VBD.BalanceID

