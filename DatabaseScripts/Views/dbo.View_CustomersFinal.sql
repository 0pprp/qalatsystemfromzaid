
create   view [dbo].[View_CustomersFinal]
as
WITH SalesData AS (
    SELECT 
        CustomerID,
        ROUND(ISNULL(SUM(AmountTotalSalesDenar), 0), -3) AS AmountTotalSales,
        ROUND(ISNULL(SUM(AmountTotalCostDenar), 0), -3) AS CostTotalSales,
        ROUND(ISNULL(SUM(AmountDaySalesDenar), 0), -3) AS AmountDaySales,
        MAX(DateCreate) AS DateSaleDevice
    FROM View_CustomersSalesDelegate
    GROUP BY CustomerID
),
PaymentData AS (
    SELECT 
        CustomerID,
        ROUND(ISNULL(SUM(AmountDenar), 0), -3) AS ReceiptsTotal
    FROM View_CustomersPaymentsDelegate
    GROUP BY CustomerID
),
AddToBoxData AS (
    SELECT 
        CustomerIDPayment,
        ISNULL(SUM(Amount), 0) AS TotalAmount
    FROM View_AddToBox
    GROUP BY CustomerIDPayment
),
PaymentDates AS (
    SELECT
        CustomerID,
        ROW_NUMBER() OVER (PARTITION BY CustomerID ORDER BY DateValue DESC) AS RowNum,
        ROUND(ISNULL(AmountDenar, 0), -3) AS AmountDenar
    FROM dbo.View_CustomersPaymentsDelegate
    JOIN WeekDateView ON CONVERT(DATE, PaymentDate) = CONVERT(DATE, DateValue)
),
ItemsData AS (
    SELECT 
        CustomerID,
        STUFF((
            SELECT + ' ( ' + '' + ItemName + '' + ' ( ' + CAST(Quantity AS NVARCHAR(255)) + ' ) ' + ' ) '
            FROM View_SelectItemsSalesItemsNames
            WHERE CustomerID = VSSI.CustomerID
            FOR XML PATH(''), TYPE).value('.', 'NVARCHAR(MAX)'), 1, 0, '') AS ItemsNames
    FROM View_SelectItemsSalesItemsNames VSSI
    GROUP BY CustomerID
)
SELECT 
    Customers.*,
    ISNULL(MAX(Delegates.DelegateName), '') AS DelegateName,
    ISNULL(MAX(Users.UserName), '') AS UserName,
    ISNULL(MAX(Cities.CityName), '') AS CityName,
    ISNULL(MAX(SD.DateSaleDevice), NULL) AS DateSaleDevice,
    ISNULL(MAX(SD.AmountTotalSales), 0) AS AmountTotalSales,
    ISNULL(MAX(SD.CostTotalSales), 0) AS CostTotalSales,
    ISNULL(MAX(SD.AmountDaySales), 0) AS AmountDaySales,
    ISNULL(MAX(PD.ReceiptsTotal), 0) AS ReceiptsTotal,
    ROUND(ISNULL(MAX(SD.AmountTotalSales), 0) - ISNULL(MAX(PD.ReceiptsTotal), 0), -3) AS AmountRemaining,
    FLOOR(
        ISNULL(
            CASE 
                WHEN ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales)) <> 0 
                THEN ((MAX(ATB.TotalAmount) / ((DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1) * MAX(SD.AmountDaySales))) * 100)
                ELSE 0
            END, 
            0
        )
    ) AS ReceiptRateDevice,
    ISNULL(DATEDIFF(DAY, MAX(SD.DateSaleDevice), GETDATE()) + 1, 0) AS NumberOfDayDevice,
    (SELECT TOP 1 PhoneNumber FROM CompanyInformation) AS PhoneNumberCompany,
    COUNT(CustomersPayments.CustomerPaymentID) + 1 AS CountReceiptDevice,
    ISNULL(MAX(ID.ItemsNames), '') AS ItemsNames,
	  (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                                    FROM            dbo.View_CustomersPaymentsDelegate
                                    WHERE        (CustomerID = dbo.Customers.CustomerID) AND (CONVERT(date, PaymentDate) =
                                                                  (SELECT        CONVERT(date, DateValue) AS DateCreate
                                                                    FROM            WeekDateView
                                                                    ORDER BY DateCreate DESC OFFSET 1 ROWS FETCH NEXT 1 ROW ONLY))) AS Amount1,
                                  (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                                    FROM            dbo.View_CustomersPaymentsDelegate AS View_CustomersPayments_6
                                    WHERE        (CustomerID = dbo.Customers.CustomerID) AND (CONVERT(date, PaymentDate) =
                                                                  (SELECT        CONVERT(date, DateValue) AS DateCreate
                                                                    FROM        WeekDateView
                                                                    ORDER BY DateCreate DESC OFFSET 2 ROWS FETCH NEXT 1 ROW ONLY))) AS Amount2,
                                  (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                                    FROM            dbo.View_CustomersPaymentsDelegate AS View_CustomersPayments_5
                                    WHERE        (CustomerID = dbo.Customers.CustomerID) AND (CONVERT(date, PaymentDate) =
                                                                  (SELECT        CONVERT(date, DateValue) AS DateCreate
                                                                    FROM            WeekDateView
                                                                    ORDER BY DateCreate DESC OFFSET 3 ROWS FETCH NEXT 1 ROW ONLY))) AS Amount3,
                                  (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                                    FROM            dbo.View_CustomersPaymentsDelegate AS View_CustomersPayments_4
                                    WHERE        (CustomerID = dbo.Customers.CustomerID) AND (CONVERT(date, PaymentDate) =
                                                                  (SELECT        CONVERT(date, DateValue) AS DateCreate
                                                                    FROM            WeekDateView
                                                                    ORDER BY DateCreate DESC OFFSET 4 ROWS FETCH NEXT 1 ROW ONLY))) AS Amount4,
                                  (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                                    FROM            dbo.View_CustomersPaymentsDelegate AS View_CustomersPayments_3
                                    WHERE        (CustomerID = dbo.Customers.CustomerID) AND (CONVERT(date, PaymentDate) =
                                                                  (SELECT        CONVERT(date, DateValue) AS DateCreate
                                                                    FROM            WeekDateView
                                                                    ORDER BY DateCreate DESC OFFSET 5 ROWS FETCH NEXT 1 ROW ONLY))) AS Amount5,
                                  (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                                    FROM            dbo.View_CustomersPaymentsDelegate AS View_CustomersPayments_2
                                    WHERE        (CustomerID = dbo.Customers.CustomerID) AND (CONVERT(date, PaymentDate) =
                                                                  (SELECT        CONVERT(date, DateValue) AS DateCreate
                                                                    FROM            WeekDateView
                                                                    ORDER BY DateCreate DESC OFFSET 6 ROWS FETCH NEXT 1 ROW ONLY))) AS Amount6,
                                  (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                                    FROM            dbo.View_CustomersPaymentsDelegate AS View_CustomersPayments_1
                                    WHERE        (CustomerID = dbo.Customers.CustomerID) AND (CONVERT(date, PaymentDate) =
                                                                  (SELECT        CONVERT(date, DateValue) AS DateCreate
                                                                    FROM            WeekDateView
                                                                    ORDER BY DateCreate DESC OFFSET 7 ROWS FETCH NEXT 1 ROW ONLY))) AS Amount7
FROM 
    dbo.Customers
LEFT JOIN 
    Delegates ON Customers.DelegateID = Delegates.DelegateID
LEFT JOIN 
    Users ON Customers.UserID = Users.UserID
LEFT JOIN 
    Cities ON Customers.CityID = Cities.CityID
LEFT JOIN 
    SalesData SD ON Customers.CustomerID = SD.CustomerID
LEFT JOIN 
    PaymentData PD ON Customers.CustomerID = PD.CustomerID
LEFT JOIN 
    AddToBoxData ATB ON Customers.CustomerID = ATB.CustomerIDPayment
LEFT JOIN 
    ItemsData ID ON Customers.CustomerID = ID.CustomerID
LEFT JOIN 
    CustomersPayments ON Customers.CustomerID = CustomersPayments.CustomerID
GROUP BY 
    Customers.CustomerID, 
    Customers.DelegateID, 
    Customers.UserID, 
    Customers.CityID, 
    Customers.CustomerName, 
    Customers.Address, 
    Customers.Longitude, 
    Customers.Latitude, 
    Customers.Notes, 
    Customers.PhoneNumber, 
    Customers.CustomerState, 
    Customers.ShopName, 
    Customers.StoreAddress, 
    Customers.NearestFunctionPoint, 
    Customers.StorePhoneNumber, 
    Customers.Neighborhood, 
    Customers.AmountReceverDay, 
    Customers.AsyncID, 
    Customers.AsyncState, 
    Customers.SelectState, 
    Customers.SaleName, 
    Customers.IsLegal, 
    Customers.ReceiptName, 
    Customers.CreatedDate, 
    Customers.UpdatedDate, 
    Customers.CustomerImage;


