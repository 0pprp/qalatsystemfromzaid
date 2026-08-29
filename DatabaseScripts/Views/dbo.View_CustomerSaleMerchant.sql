create   view [dbo].[View_CustomerSaleMerchant]
AS
SELECT 
    CustomersSales.CustomerSaleID, 
    CustomersSales.MerchantID, 
    CustomersSales.DateCreate, 
    Merchant.MerchantName,
    ISNULL(SUM(View_SelectItemsSales.ItemCostDenar * View_SelectItemsSales.Quantity), 0) AS AmountTotalCostDenar,
    STUFF(
        (
            SELECT 
                ' ( ' + '' + ItemName + '' + ' ( ' + CAST(Quantity AS NVARCHAR(255)) + ' ) ' + ' ) '
            FROM 
                View_SelectItemsSales 
            WHERE 
                View_SelectItemsSales.CustomerSaleID = CustomersSales.CustomerSaleID
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 
        1, 
        0, 
        ''
    ) AS ItemsNames
FROM 
    CustomersSales
LEFT JOIN 
    Merchant ON Merchant.MerchantID = CustomersSales.MerchantID
LEFT JOIN 
    View_SelectItemsSales ON View_SelectItemsSales.CustomerSaleID = CustomersSales.CustomerSaleID
GROUP BY 
    CustomersSales.CustomerSaleID, 
    CustomersSales.MerchantID, 
    CustomersSales.DateCreate, 
    Merchant.MerchantName;


