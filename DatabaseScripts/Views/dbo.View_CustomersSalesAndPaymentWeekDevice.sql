
create   view [dbo].[View_CustomersSalesAndPaymentWeekDevice]
AS
WITH CustomerData AS (
    SELECT 
        CustomerID, 
        CityID, 
        CustomerName, 
        SaleName, 
        PhoneNumber
    FROM 
        dbo.Customers
),
CityData AS (
    SELECT 
        CityID, 
        CityName
    FROM 
        dbo.Cities
),
UserData AS (
    SELECT 
        UserID, 
        UserName
    FROM 
        dbo.Users
),
StoreData AS (
    SELECT 
        StoreID, 
        StoreName
    FROM 
        dbo.Stores
),
DelegateData AS (
    SELECT 
        DelegateID, 
        DelegateName
    FROM 
        dbo.Delegates
),
ItemsSalesData AS (
    SELECT 
        dbo.View_SelectItemsSales.CustomerSaleID, 
        ISNULL(SUM(Quantity), 0) AS NumberOfItemsSales,
        ISNULL(SUM(ItemPriceDenar * Quantity), 0) AS AmountTotalDenar,
        ISNULL(SUM(AmountDayDenar * Quantity), 0) AS AmountTotalDayDenar,
        ISNULL(SUM(ItemPriceDenar * Quantity), 0) - MAX(CustomersSales.DiscountAmountTotal * 1448) AS AmountTotalSalesDenar,
        ISNULL(SUM(AmountDayDenar * Quantity), 0) - MAX(CustomersSales.DiscountAmountTotalDay * 1448) AS AmountDaySalesDenar,
        ISNULL(SUM(ItemCostDenar * Quantity), 0) AS AmountTotalCostDenar
    FROM 
        dbo.View_SelectItemsSales
    JOIN 
        dbo.CustomersSales ON dbo.View_SelectItemsSales.CustomerSaleID = dbo.CustomersSales.CustomerSaleID
    GROUP BY 
        dbo.View_SelectItemsSales.CustomerSaleID
),
ReceiptsData AS (
    SELECT 
        CustomerIDPayment, 
        ISNULL(SUM(AmountDenar), 0) AS ReceiptsTotal
    FROM 
        dbo.View_AddToBox
    GROUP BY 
        CustomerIDPayment
),
ItemsNamesData AS (
    SELECT 
        dbo.View_SelectItemsSalesItemsNames.CustomerSaleID,
        STUFF((
            SELECT 
                ' ( ' + '' + ItemName + '' + ' ( ' + CAST(Quantity AS NVARCHAR(255)) + ' ) ' + ' ) '
            FROM 
                dbo.View_SelectItemsSalesItemsNames AS InnerItems
            WHERE 
                InnerItems.CustomerSaleID = dbo.View_SelectItemsSalesItemsNames.CustomerSaleID
            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 0, '') AS ItemsNames
    FROM 
        dbo.View_SelectItemsSalesItemsNames
    GROUP BY 
        dbo.View_SelectItemsSalesItemsNames.CustomerSaleID
),
PaymentsData AS (
    SELECT 
        CustomerID, 
        COUNT(*) AS CountReceiptDevice,
        MAX(PaymentDate) AS LastPaymentDate
    FROM 
        dbo.CustomersPayments
    GROUP BY 
        CustomerID
)
SELECT 
    dbo.CustomersSales.CustomerSaleID,
    dbo.CustomersSales.UserID,
    dbo.CustomersSales.CustomerID,
    dbo.CustomersSales.Notes,
    dbo.CustomersSales.DateCreate,
    dbo.CustomersSales.DateModify,
    dbo.CustomersSales.BoundNumber,
    dbo.CustomersSales.StoreID,
    dbo.CustomersSales.DelegateID,
    dbo.CustomersSales.AccountZero,
    dbo.CustomersSales.DelegateState,
    dbo.CustomersSales.DiscountAmountTotal,
    dbo.CustomersSales.DiscountAmountTotalDay,
    dbo.CustomersSales.AsyncState,
    dbo.CustomersSales.AsyncID,
    dbo.CustomersSales.DiscountAmountTotalTwoWay,
    dbo.CustomersSales.DiscountAmountDayTotalTwoWay,
    dbo.CustomersSales.MerchantID,
    CustomerData.CityID,
    CityData.CityName,
    UserData.UserName,
    CustomerData.CustomerName,
    CustomerData.SaleName,
    CustomerData.PhoneNumber,
    StoreData.StoreName,
    DelegateData.DelegateName,
    ItemsSalesData.NumberOfItemsSales,
    ItemsSalesData.AmountTotalDenar,
    ItemsSalesData.AmountTotalDayDenar,
    ItemsSalesData.AmountTotalSalesDenar,
    ItemsSalesData.AmountDaySalesDenar,
    ItemsSalesData.AmountTotalCostDenar,
    ReceiptsData.ReceiptsTotal,
    ROUND(
        ItemsSalesData.AmountTotalDenar - ISNULL(ReceiptsData.ReceiptsTotal, 0),
        -3
    ) AS AmountRemaining,
    ItemsNamesData.ItemsNames,
    PaymentsData.CountReceiptDevice,
    PaymentsData.LastPaymentDate,
	   (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments
                               WHERE        (CustomerID = dbo.CustomersSales.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 1) AS date))) AS Amount1,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_6
                               WHERE        (CustomerID = dbo.CustomersSales.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 2) AS date))) AS Amount2,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_5
                               WHERE        (CustomerID = dbo.CustomersSales.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 3) AS date))) AS Amount3,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_4
                               WHERE        (CustomerID = dbo.CustomersSales.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 4) AS date))) AS Amount4,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_3
                               WHERE        (CustomerID = dbo.CustomersSales.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 5) AS date))) AS Amount5,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_2
                               WHERE        (CustomerID = dbo.CustomersSales.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 6) AS date))) AS Amount6,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.CustomersSales.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 7) AS date))) AS Amount7 
FROM 
    dbo.CustomersSales
LEFT JOIN 
    CustomerData ON dbo.CustomersSales.CustomerID = CustomerData.CustomerID
LEFT JOIN 
    CityData ON CustomerData.CityID = CityData.CityID
LEFT JOIN 
    UserData ON dbo.CustomersSales.UserID = UserData.UserID
LEFT JOIN 
    StoreData ON dbo.CustomersSales.StoreID = StoreData.StoreID
LEFT JOIN 
    DelegateData ON dbo.CustomersSales.DelegateID = DelegateData.DelegateID
LEFT JOIN 
    ItemsSalesData ON dbo.CustomersSales.CustomerSaleID = ItemsSalesData.CustomerSaleID
LEFT JOIN 
    ReceiptsData ON dbo.CustomersSales.CustomerID = ReceiptsData.CustomerIDPayment
LEFT JOIN 
    ItemsNamesData ON dbo.CustomersSales.CustomerSaleID = ItemsNamesData.CustomerSaleID
LEFT JOIN 
    PaymentsData ON dbo.CustomersSales.CustomerID = PaymentsData.CustomerID;


