create   view [dbo].[View_CustomersWithReceiptsHundredDay]
as
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
        ISNULL(SUM(AmountTotalSalesDenar), 0) AS AmountTotalSalesDenar,
        ISNULL(SUM(AmountDaySalesDenar), 0) AS AmountDaySales
    FROM 
        View_CustomersSales
    GROUP BY 
        CustomerID
),
CustomerPaymentsData AS (
    SELECT 
        CustomerID,
        ISNULL(SUM(AmountDenar), 0) AS ReceiptsTotal
    FROM 
        View_CustomersPayments
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
                View_SelectItemsSales
            WHERE 
                CustomerID = OuterTable.CustomerID
            FOR XML PATH(''), TYPE
        ).value('.', 'NVARCHAR(MAX)'), 1, 0, '') AS ItemsNames
    FROM 
        Customers AS OuterTable
    GROUP BY 
        OuterTable.CustomerID
),
LastPaymentData AS (
    SELECT 
        CustomerID,
        MAX(PaymentDate) AS LastPaymentDate
    FROM 
        CustomersPayments
    GROUP BY 
        CustomerID
),
AmountByDayData AS (
    SELECT 
        CustomerID,
        CONVERT(DATE, PaymentDate) AS PaymentDate,
        ISNULL(SUM(AmountDenar), 0) AS AmountDenar
    FROM 
        View_CustomersPayments
    GROUP BY 
        CustomerID, CONVERT(DATE, PaymentDate)
),
AccumulatedData AS (
    SELECT 
        CustomerID,
        FLOOR(ISNULL(
            (
                (SELECT ISNULL(SUM(AmountDenar), 0) FROM dbo.View_AddToBox WHERE CustomerIDPayment = Customers.CustomerID) /
                (
                    (DATEDIFF(DAY, 
                        (SELECT TOP 1 DateCreate FROM CustomersSales WHERE CustomerID = Customers.CustomerID), GETDATE()) + 1) *
                    (SELECT ISNULL(SUM(AmountDaySalesDenar), 0) FROM View_CustomersSales WHERE CustomerID = Customers.CustomerID)
                )
            ) * 100, 0)) AS ReceiptRateDevice,
        ISNULL(
            DATEDIFF(DAY, 
                (SELECT TOP 1 DateCreate FROM CustomersSales WHERE CustomerID = Customers.CustomerID), GETDATE()) + 1, 0
        ) AS NumberOfDayDevice,
        (
            ISNULL(
                DATEDIFF(DAY, 
                    (SELECT TOP 1 DateCreate FROM CustomersSales WHERE CustomerID = Customers.CustomerID), GETDATE()) + 1, 0
            ) * 
            (SELECT ISNULL(SUM(AmountDaySalesDenar), 0) FROM View_CustomersSales WHERE CustomerID = Customers.CustomerID)
        ) - 
        (SELECT ISNULL(SUM(AmountDenar), 0) FROM dbo.View_AddToBox WHERE CustomerIDPayment = Customers.CustomerID) AS AccumulatedDevice
    FROM 
        Customers
)
SELECT 
    Customers.CustomerID,
    Customers.DelegateID,
    Customers.CustomerName,
    Customers.PhoneNumber,
    DelegateData.DelegateName,
    CustomerSalesData.AmountTotalSalesDenar,
    CustomerSalesData.AmountDaySales,
    CustomerPaymentsData.ReceiptsTotal,
    ROUND(CustomerSalesData.AmountTotalSalesDenar - CustomerPaymentsData.ReceiptsTotal, -3) AS AmountRemaining,
    ItemsNamesData.ItemsNames,
    (SELECT TOP 1 DateCreate FROM CustomersSales WHERE CustomerID = Customers.CustomerID) AS DateSale,
    LastPaymentData.LastPaymentDate,
    AccumulatedData.ReceiptRateDevice,
    AccumulatedData.NumberOfDayDevice,
    AccumulatedData.AccumulatedDevice,
	  (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 1)))) AS Amount1,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_6
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 2)))) AS Amount2,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_5
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 3)))) AS Amount3,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_4
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 4)))) AS Amount4,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_3
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 5)))) AS Amount5,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_2
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 6)))) AS Amount6,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 7)))) AS Amount7,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 8)))) AS Amount8,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 9)))) AS Amount9,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 10)))) AS Amount10,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 11)))) AS Amount11,
                   (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 12)))) AS Amount12,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 13)))) AS Amount13,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 14)))) AS Amount14,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 15)))) AS Amount15,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 16)))) AS Amount16,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 17)))) AS Amount17,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 18)))) AS Amount18,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 19)))) AS Amount19,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 20)))) AS Amount20,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 21)))) AS Amount21,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 22)))) AS Amount22,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 23)))) AS Amount23,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 24)))) AS Amount24,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 25)))) AS Amount25,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 26)))) AS Amount26,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 27)))) AS Amount27,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 28)))) AS Amount28,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 29)))) AS Amount29,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 30)))) AS Amount30,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM     dbo.View_CustomersPayments
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 31)))) AS Amount31,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_6
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 32)))) AS Amount32,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_5
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 33)))) AS Amount33,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_4
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 34)))) AS Amount34,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_3
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 35)))) AS Amount35,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_2
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 36)))) AS Amount36,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 37)))) AS Amount37,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 38)))) AS Amount38,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 39)))) AS Amount39,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 40)))) AS Amount40,
                          (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 41)))) AS Amount41,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 42)))) AS Amount42,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 43)))) AS Amount43,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 44)))) AS Amount44,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 45)))) AS Amount45,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 46)))) AS Amount46,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 47)))) AS Amount47,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 48)))) AS Amount48,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 49)))) AS Amount49,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 50)))) AS Amount50,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 51)))) AS Amount51,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 52)))) AS Amount52,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 53)))) AS Amount53,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 54)))) AS Amount54,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 55)))) AS Amount55,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 56)))) AS Amount56,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 57)))) AS Amount57,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 58)))) AS Amount58,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 59)))) AS Amount59,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 60)))) AS Amount60,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 61)))) AS Amount61,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_6
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 62)))) AS Amount62,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_5
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 63)))) AS Amount63,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_4
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 64)))) AS Amount64,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_3
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 65)))) AS Amount65,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_2
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 66)))) AS Amount66,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 67)))) AS Amount67,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 68)))) AS Amount68,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 69)))) AS Amount69,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 70)))) AS Amount70,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 71)))) AS Amount71,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 72)))) AS Amount72,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 73)))) AS Amount73,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 74)))) AS Amount74,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 75)))) AS Amount75,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 76)))) AS Amount76,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 77)))) AS Amount77,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 78)))) AS Amount78,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 79)))) AS Amount79,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 80)))) AS Amount80,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 81)))) AS Amount81,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 82)))) AS Amount82,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 83)))) AS Amount83,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 84)))) AS Amount84,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 85)))) AS Amount85,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 86)))) AS Amount86,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 87)))) AS Amount87,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 88)))) AS Amount88,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
  FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 89)))) AS Amount89,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 90)))) AS Amount90,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 91)))) AS Amount91,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_6
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 92)))) AS Amount92,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_5
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 93)))) AS Amount93,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_4
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 94)))) AS Amount94,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_3
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 95)))) AS Amount95,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_2
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 96)))) AS Amount96,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 97)))) AS Amount97,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 98)))) AS Amount98,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 99)))) AS Amount99,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 100)))) AS Amount100
FROM 
    Customers
LEFT JOIN 
    DelegateData ON Customers.DelegateID = DelegateData.DelegateID
LEFT JOIN 
    CustomerSalesData ON Customers.CustomerID = CustomerSalesData.CustomerID
LEFT JOIN 
    CustomerPaymentsData ON Customers.CustomerID = CustomerPaymentsData.CustomerID
LEFT JOIN 
    ItemsNamesData ON Customers.CustomerID = ItemsNamesData.CustomerID
LEFT JOIN 
    LastPaymentData ON Customers.CustomerID = LastPaymentData.CustomerID
LEFT JOIN 
    AccumulatedData ON Customers.CustomerID = AccumulatedData.CustomerID
WHERE 
    ROUND(CustomerSalesData.AmountTotalSalesDenar - CustomerPaymentsData.ReceiptsTotal, -3) > 0;


