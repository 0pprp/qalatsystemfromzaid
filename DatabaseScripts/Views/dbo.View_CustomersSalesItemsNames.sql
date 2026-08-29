create   view [dbo].[View_CustomersSalesItemsNames]
AS
WITH ItemsNamesData AS (
    SELECT 
        dbo.View_SelectItemsSales.CustomerSaleID,
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
        dbo.View_SelectItemsSales.CustomerSaleID
)
SELECT 
    dbo.CustomersSales.CustomerSaleID,
    dbo.CustomersSales.CustomerID,
    dbo.CustomersSales.DateCreate,
    dbo.CustomersSales.DelegateID,
    ItemsNamesData.ItemsNames
FROM 
    dbo.CustomersSales
LEFT JOIN 
    ItemsNamesData ON dbo.CustomersSales.CustomerSaleID = ItemsNamesData.CustomerSaleID;


