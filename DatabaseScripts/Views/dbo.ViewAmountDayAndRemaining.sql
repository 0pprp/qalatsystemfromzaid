create   view [dbo].[ViewAmountDayAndRemaining]
AS
WITH SalesData AS (SELECT        CS.CustomerSaleID, CS.DateCreate, CS.DelegateID, CS.CustomerID, ISNULL(SUM(VS.AmountDayDenar * VS.Quantity), 0) - CS.DiscountAmountTotalDay * 1448 AS AmountDaySalesDenar, 
                                                                      ROUND(ISNULL(TotalSales.TotalAmount - TotalPayments.TotalPaid, 0), - 3) AS AmountRemaining
                                             FROM            dbo.CustomersSales AS CS LEFT OUTER JOIN
                                                                      dbo.View_SelectItemsSales AS VS ON CS.CustomerSaleID = VS.CustomerSaleID LEFT OUTER JOIN
                                                                          (SELECT        CustomerID, SUM(AmountTotalSalesDenar) AS TotalAmount
                                                                            FROM            dbo.View_CustomersSales
                                                                            GROUP BY CustomerID) AS TotalSales ON CS.CustomerID = TotalSales.CustomerID LEFT OUTER JOIN
                                                                          (SELECT        CustomerIDPayment, SUM(AmountDenar) AS TotalPaid
                                                                            FROM            dbo.View_AddToBox
                                                                            GROUP BY CustomerIDPayment) AS TotalPayments ON CS.CustomerID = TotalPayments.CustomerIDPayment
                                             GROUP BY CS.CustomerSaleID, CS.DateCreate, CS.DelegateID, CS.CustomerID, CS.DiscountAmountTotalDay, TotalSales.TotalAmount, TotalPayments.TotalPaid)
    SELECT        SD.CustomerSaleID, SD.DateCreate, SD.DelegateID, SD.CustomerID, SD.AmountDaySalesDenar, SD.AmountRemaining, C.IsLegal
     FROM            SalesData AS SD INNER JOIN
                              dbo.Customers AS C ON SD.CustomerID = C.CustomerID
     WHERE        (C.IsLegal = 'false') AND (SD.AmountRemaining > 0)

