create   view [dbo].[View_Merchant]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users
                                           WHERE        (UserState = 'true')), CityData AS
    (SELECT        CityID, CityName
      FROM            dbo.Cities), SalesSummary AS
    (SELECT        MerchantID, ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), - 3) AS AmountTotalSales, ROUND(ISNULL(SUM(AmountTotalCostDenar), 0), - 3) AS CostTotalSales, ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), - 3) 
                                AS AmountDaySales, COUNT(*) AS NumberOfSale
      FROM            dbo.View_CustomersSalesMerchant
      GROUP BY MerchantID)
    SELECT        dbo.Merchant.MerchantID, dbo.Merchant.UserID, dbo.Merchant.CityID, dbo.Merchant.MerchantName, dbo.Merchant.ManagerName, dbo.Merchant.Password, dbo.Merchant.PhoneNumberMerchant, 
                              dbo.Merchant.PhoneNumberManager, dbo.Merchant.Address, dbo.Merchant.ImageManger, dbo.Merchant.ImageMerchant, dbo.Merchant.MerchantState, dbo.Merchant.AsyncID, dbo.Merchant.AsyncState, 
                              UserData_1.UserName, CityData_1.CityName, ISNULL(SalesSummary_1.AmountTotalSales, 0) AS AmountTotalSales, ISNULL(SalesSummary_1.CostTotalSales, 0) AS CostTotalSales, 
                              ISNULL(SalesSummary_1.AmountDaySales, 0) AS AmountDaySales, ISNULL(SalesSummary_1.NumberOfSale, 0) AS NumberOfSale
     FROM            dbo.Merchant LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.Merchant.UserID = UserData_1.UserID LEFT OUTER JOIN
                              CityData AS CityData_1 ON dbo.Merchant.CityID = CityData_1.CityID LEFT OUTER JOIN
                              SalesSummary AS SalesSummary_1 ON dbo.Merchant.MerchantID = SalesSummary_1.MerchantID
     WHERE        (dbo.Merchant.MerchantState = 'true')

