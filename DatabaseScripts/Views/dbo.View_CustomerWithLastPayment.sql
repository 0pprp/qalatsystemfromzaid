create   view [dbo].[View_CustomerWithLastPayment]
AS
WITH DelegateData AS (
    SELECT 
        DelegateID, 
        DelegateName
    FROM 
        Delegates
),
CustomerSalesData AS (
    SELECT 
        CustomerID,
        MAX(DateCreate) AS DateSaleDevice,
        ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), -3) AS AmountTotalSales,
        ROUND(ISNULL(SUM(AmountTotalCostDenar), 0), -3) AS CostTotalSales,
        ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), -3) AS AmountDaySales
    FROM 
        View_CustomersSalesDelegate
    GROUP BY 
        CustomerID
),
CustomerPaymentsData AS (
    SELECT 
        CustomerID,
        ROUND(ISNULL(SUM(AmountDenar), 0), -3) AS ReceiptsTotal
    FROM 
        View_CustomersPaymentsDelegate
    GROUP BY 
        CustomerID
),
ItemsNamesData AS (
    SELECT 
        CustomerID,
        STUFF((
            SELECT 
                ' ( ' + ItemName + ' ( ' + CAST(Quantity AS NVARCHAR(255)) + ' ) ) '
            FROM 
                View_SelectItemsSalesItemsNames AS InnerItems
            WHERE 
                InnerItems.CustomerID = OuterTable.CustomerID
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 0, '') AS ItemsNames
    FROM 
        dbo.Customers AS OuterTable
    GROUP BY 
        OuterTable.CustomerID
),
LastPaymentData AS (
    SELECT 
        CustomerID,
        MAX(LastPayment) AS LastDatePayment
    FROM 
        View_LastPayment
    GROUP BY 
        CustomerID
)
SELECT 
    Customers.CustomerID,
    Customers.CustomerName,
    Customers.PhoneNumber,
    Customers.Address,
    Customers.ShopName,
    DelegateData.DelegateName,
    CustomerSalesData.DateSaleDevice,
    CustomerSalesData.AmountTotalSales,
    CustomerSalesData.CostTotalSales,
    CustomerSalesData.AmountDaySales,
    CustomerPaymentsData.ReceiptsTotal,
    ItemsNamesData.ItemsNames,
    LastPaymentData.LastDatePayment
FROM 
    dbo.Customers
LEFT JOIN 
    DelegateData ON Customers.DelegateID = DelegateData.DelegateID
LEFT JOIN 
    CustomerSalesData ON Customers.CustomerID = CustomerSalesData.CustomerID
LEFT JOIN 
    CustomerPaymentsData ON Customers.CustomerID = CustomerPaymentsData.CustomerID
LEFT JOIN 
    ItemsNamesData ON Customers.CustomerID = ItemsNamesData.CustomerID
LEFT JOIN 
    LastPaymentData ON Customers.CustomerID = LastPaymentData.CustomerID;


