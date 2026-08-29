create   view [dbo].[View_SelectItemsSaleBalanceTemporaryRequest]
AS
WITH BalanceData AS (SELECT        BalanceID, BalanceName
                                                  FROM            dbo.Balance), ViewBalanceData AS
    (SELECT        BalanceID, ISNULL(BalancePriceDenar, 0) AS ItemPriceDenar, ISNULL(BalanceCostDenar, 0) AS ItemCostDenar, ISNULL(AmountDayDenar, 0) AS AmountDayDenar
      FROM            dbo.View_Balance)
    SELECT        SISBTR.SelectItemsSaleBalanceTemporaryRequestID, SISBTR.DelegateID, SISBTR.BalanceID, SISBTR.AsyncState, SISBTR.AsyncID, BD.BalanceName AS ItemName, VBD.ItemPriceDenar, VBD.ItemCostDenar, 
                              VBD.AmountDayDenar
     FROM            dbo.SelectItemsSaleBalanceTemporaryRequest AS SISBTR LEFT OUTER JOIN
                              BalanceData AS BD ON SISBTR.BalanceID = BD.BalanceID LEFT OUTER JOIN
                              ViewBalanceData AS VBD ON SISBTR.BalanceID = VBD.BalanceID

