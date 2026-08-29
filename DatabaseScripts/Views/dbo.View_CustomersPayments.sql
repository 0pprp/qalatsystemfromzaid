 
CREATE   view [dbo].[View_CustomersPayments]
as
WITH UserData AS (
    SELECT 
        UserID, 
        UserName
    FROM 
        dbo.Users
),
CustomerData AS (
    SELECT 
        CustomerID, 
        CustomerName, 
        PhoneNumber, 
        CityID
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
BoxData AS (
    SELECT 
        BoxID, 
        BoxName
    FROM 
        dbo.Boxes
),
DelegateData AS (
    SELECT 
        DelegateID, 
        DelegateName
    FROM 
        dbo.Delegates
),
AmountData AS (
    SELECT 
        CustomerPaymentID, 
        ISNULL(SUM(Amount * 1448), 0) AS AmountDenar
    FROM 
        dbo.AddToBox
    GROUP BY 
        CustomerPaymentID
),
SalesData AS (
    SELECT 
        CustomerID, 
        ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), -3) AS AmountDaySales
    FROM 
        dbo.View_CustomersSalesDelegate
    GROUP BY 
        CustomerID
),
ItemsData AS (
    SELECT 
        CustomerID,
        ('') AS ItemsNames
    FROM 
        dbo.View_SelectItemsSalesItemsNames AS VSSI
    GROUP BY 
        VSSI.CustomerID
)
SELECT 
    CustomersPayments.CustomerPaymentID,
    CustomersPayments.UserID,
    CustomersPayments.CustomerID,
    CustomersPayments.BoxID,
    CustomersPayments.PaymentDate,
    CustomersPayments.BoundNumber,
    CustomersPayments.DelegateID,
    CustomersPayments.AccountZero,
    CustomersPayments.DelegateState,
    CustomersPayments.AsyncState,
    CustomersPayments.AsyncID,
    CustomersPayments.SelectState,
    CustomersPayments.Location,
    UserData.UserName,
    CustomerData.CustomerName,
    CustomerData.PhoneNumber,
    BoxData.BoxName,
    DelegateData.DelegateName,
    AmountData.AmountDenar,
    SalesData.AmountDaySales,
    CustomerData.CityID,
    CityData.CityName,
    ItemsData.ItemsNames
FROM 
    dbo.CustomersPayments
LEFT JOIN 
    UserData ON CustomersPayments.UserID = UserData.UserID
LEFT JOIN 
    CustomerData ON CustomersPayments.CustomerID = CustomerData.CustomerID
LEFT JOIN 
    CityData ON CustomerData.CityID = CityData.CityID
LEFT JOIN 
    BoxData ON CustomersPayments.BoxID = BoxData.BoxID
LEFT JOIN 
    DelegateData ON CustomersPayments.DelegateID = DelegateData.DelegateID
LEFT JOIN 
    AmountData ON CustomersPayments.CustomerPaymentID = AmountData.CustomerPaymentID
LEFT JOIN 
    SalesData ON CustomersPayments.CustomerID = SalesData.CustomerID
LEFT JOIN 
    ItemsData ON CustomersPayments.CustomerID = ItemsData.CustomerID
 


