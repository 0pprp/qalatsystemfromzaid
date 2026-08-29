create   view [dbo].[View_SelectItemsSaleBalanceRequest]
AS
SELECT        SelectItemsSaleBalanceRequestID, CustomerSaleBalanceRequestID, BalanceID, AsyncState, AsyncID,
                             (SELECT        BalanceName
                               FROM            dbo.Balance
                               WHERE        (BalanceID = dbo.SelectItemsSaleBalanceRequest.BalanceID)) AS ItemName,
                             (SELECT        ISNULL(BalancePriceDenar, 0) AS Expr1
                               FROM            dbo.View_Balance
                               WHERE        (BalanceID = dbo.SelectItemsSaleBalanceRequest.BalanceID)) AS ItemPriceDenar,
                             (SELECT        ISNULL(BalanceCostDenar, 0) AS Expr1
                               FROM            dbo.View_Balance AS View_Balance_2
                               WHERE        (BalanceID = dbo.SelectItemsSaleBalanceRequest.BalanceID)) AS ItemCostDenar,
                             (SELECT        ISNULL(AmountDayDenar, 0) AS Expr1
                               FROM            dbo.View_Balance AS View_Balance_1
                               WHERE        (BalanceID = dbo.SelectItemsSaleBalanceRequest.BalanceID)) AS AmountDayDenar
FROM            dbo.SelectItemsSaleBalanceRequest

