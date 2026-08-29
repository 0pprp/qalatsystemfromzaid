create   view [dbo].[View_CustomersSalesMerchant]
AS
WITH ItemsSalesData AS (
    SELECT 
        CustomerSaleID,
        ISNULL(SUM(Quantity), 0) AS NumberOfItemsSales,
        ISNULL(SUM(ItemPriceDenar * Quantity), 0) AS AmountTotalDenar,
        ISNULL(SUM(AmountDayDenar * Quantity), 0) AS AmountTotalDayDenar,
        ISNULL(SUM(ItemCostDenar * Quantity), 0) AS AmountTotalCostDenar
    FROM 
        dbo.View_SelectItemsSales
    GROUP BY 
        CustomerSaleID
),
ItemsNamesData AS (
    SELECT 
        CustomerSaleID,
        STUFF((
            SELECT 
                ' ( ' + '' + ItemName + '' + ' ( ' + CAST(Quantity AS NVARCHAR(255)) + ' ) ' + ' ) '
            FROM 
                dbo.View_SelectItemsSales AS InnerItems
            WHERE 
                InnerItems.CustomerSaleID = dbo.View_SelectItemsSales.CustomerSaleID
            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 
            1, 0, ''
        ) AS ItemsNames
    FROM 
        dbo.View_SelectItemsSales
    GROUP BY 
        CustomerSaleID
)
SELECT 
    dbo.CustomersSales.CustomerSaleID,
    dbo.CustomersSales.DateCreate,
    dbo.CustomersSales.DelegateID,
    dbo.CustomersSales.CustomerID,
    dbo.CustomersSales.MerchantID,
    ISNULL(ItemsSalesData.NumberOfItemsSales, 0) AS NumberOfItemsSales,
    ISNULL(ItemsSalesData.AmountTotalDenar, 0) - dbo.CustomersSales.DiscountAmountTotal * 1448 AS AmountTotalSalesDenar,
    ISNULL(ItemsSalesData.AmountTotalDayDenar, 0) - dbo.CustomersSales.DiscountAmountTotalDay * 1448 AS AmountDaySalesDenar,
    ISNULL(ItemsSalesData.AmountTotalCostDenar, 0) AS AmountTotalCostDenar,
    ItemsNamesData.ItemsNames
FROM 
    dbo.CustomersSales
LEFT JOIN 
    ItemsSalesData ON dbo.CustomersSales.CustomerSaleID = ItemsSalesData.CustomerSaleID
LEFT JOIN 
    ItemsNamesData ON dbo.CustomersSales.CustomerSaleID = ItemsNamesData.CustomerSaleID;


