create   view [dbo].[View_ThreeMonth]
AS
WITH WeekDates AS (
    SELECT 
        CONVERT(date, DateValue) AS DateCreate,
        ROW_NUMBER() OVER (ORDER BY DateValue DESC) AS RowNum
    FROM WeekDateView
),
CustomerPayments AS (
    SELECT 
        CustomerID, 
        CONVERT(date, PaymentDate) AS PaymentDate, 
        ISNULL(SUM(AmountDenar), 0) AS TotalAmount
    FROM dbo.View_CustomersPaymentsDelegate
    GROUP BY CustomerID, CONVERT(date, PaymentDate)
)
SELECT 
    c.CustomerID,
        ISNULL(SUM(CASE WHEN wd.RowNum = 1 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount1,
    ISNULL(SUM(CASE WHEN wd.RowNum = 2 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount2,
    ISNULL(SUM(CASE WHEN wd.RowNum = 3 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount3,
    ISNULL(SUM(CASE WHEN wd.RowNum = 4 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount4,
    ISNULL(SUM(CASE WHEN wd.RowNum = 5 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount5,
    ISNULL(SUM(CASE WHEN wd.RowNum = 6 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount6,
    ISNULL(SUM(CASE WHEN wd.RowNum = 7 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount7,
    ISNULL(SUM(CASE WHEN wd.RowNum = 8 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount8,
    ISNULL(SUM(CASE WHEN wd.RowNum = 9 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount9,
    ISNULL(SUM(CASE WHEN wd.RowNum = 10 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount10,
    ISNULL(SUM(CASE WHEN wd.RowNum = 11 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount11,
    ISNULL(SUM(CASE WHEN wd.RowNum = 12 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount12,
    ISNULL(SUM(CASE WHEN wd.RowNum = 13 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount13,
    ISNULL(SUM(CASE WHEN wd.RowNum = 14 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount14,
    ISNULL(SUM(CASE WHEN wd.RowNum = 15 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount15,
    ISNULL(SUM(CASE WHEN wd.RowNum = 16 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount16,
    ISNULL(SUM(CASE WHEN wd.RowNum = 17 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount17,
    ISNULL(SUM(CASE WHEN wd.RowNum = 18 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount18,
    ISNULL(SUM(CASE WHEN wd.RowNum = 19 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount19,
    ISNULL(SUM(CASE WHEN wd.RowNum = 20 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount20,
    ISNULL(SUM(CASE WHEN wd.RowNum = 21 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount21,
    ISNULL(SUM(CASE WHEN wd.RowNum = 22 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount22,
    ISNULL(SUM(CASE WHEN wd.RowNum = 23 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount23,
    ISNULL(SUM(CASE WHEN wd.RowNum = 24 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount24,
    ISNULL(SUM(CASE WHEN wd.RowNum = 25 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount25,
    ISNULL(SUM(CASE WHEN wd.RowNum = 26 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount26,
    ISNULL(SUM(CASE WHEN wd.RowNum = 27 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount27,
    ISNULL(SUM(CASE WHEN wd.RowNum = 28 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount28,
    ISNULL(SUM(CASE WHEN wd.RowNum = 29 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount29,
    ISNULL(SUM(CASE WHEN wd.RowNum = 30 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount30,
    ISNULL(SUM(CASE WHEN wd.RowNum = 31 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount31,
    ISNULL(SUM(CASE WHEN wd.RowNum = 32 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount32,
    ISNULL(SUM(CASE WHEN wd.RowNum = 33 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount33,
    ISNULL(SUM(CASE WHEN wd.RowNum = 34 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount34,
    ISNULL(SUM(CASE WHEN wd.RowNum = 35 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount35,
    ISNULL(SUM(CASE WHEN wd.RowNum = 36 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount36,
    ISNULL(SUM(CASE WHEN wd.RowNum = 37 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount37,
    ISNULL(SUM(CASE WHEN wd.RowNum = 38 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount38,
    ISNULL(SUM(CASE WHEN wd.RowNum = 39 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount39,
    ISNULL(SUM(CASE WHEN wd.RowNum = 40 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount40,
    ISNULL(SUM(CASE WHEN wd.RowNum = 41 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount41,
    ISNULL(SUM(CASE WHEN wd.RowNum = 42 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount42,
    ISNULL(SUM(CASE WHEN wd.RowNum = 43 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount43,
    ISNULL(SUM(CASE WHEN wd.RowNum = 44 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount44,
    ISNULL(SUM(CASE WHEN wd.RowNum = 45 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount45,
    ISNULL(SUM(CASE WHEN wd.RowNum = 46 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount46,
    ISNULL(SUM(CASE WHEN wd.RowNum = 47 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount47,
    ISNULL(SUM(CASE WHEN wd.RowNum = 48 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount48,
    ISNULL(SUM(CASE WHEN wd.RowNum = 49 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount49,
    ISNULL(SUM(CASE WHEN wd.RowNum = 50 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount50,
    ISNULL(SUM(CASE WHEN wd.RowNum = 51 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount51,
    ISNULL(SUM(CASE WHEN wd.RowNum = 52 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount52,
    ISNULL(SUM(CASE WHEN wd.RowNum = 53 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount53,
    ISNULL(SUM(CASE WHEN wd.RowNum = 54 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount54,
    ISNULL(SUM(CASE WHEN wd.RowNum = 55 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount55,
    ISNULL(SUM(CASE WHEN wd.RowNum = 56 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount56,
    ISNULL(SUM(CASE WHEN wd.RowNum = 57 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount57,
    ISNULL(SUM(CASE WHEN wd.RowNum = 58 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount58,
    ISNULL(SUM(CASE WHEN wd.RowNum = 59 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount59,
    ISNULL(SUM(CASE WHEN wd.RowNum = 60 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount60,
    ISNULL(SUM(CASE WHEN wd.RowNum = 61 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount61,
    ISNULL(SUM(CASE WHEN wd.RowNum = 62 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount62,
    ISNULL(SUM(CASE WHEN wd.RowNum = 63 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount63,
    ISNULL(SUM(CASE WHEN wd.RowNum = 64 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount64,
    ISNULL(SUM(CASE WHEN wd.RowNum = 65 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount65,
    ISNULL(SUM(CASE WHEN wd.RowNum = 66 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount66,
    ISNULL(SUM(CASE WHEN wd.RowNum = 67 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount67,
    ISNULL(SUM(CASE WHEN wd.RowNum = 68 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount68,
    ISNULL(SUM(CASE WHEN wd.RowNum = 69 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount69,
    ISNULL(SUM(CASE WHEN wd.RowNum = 70 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount70,
    ISNULL(SUM(CASE WHEN wd.RowNum = 71 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount71,
    ISNULL(SUM(CASE WHEN wd.RowNum = 72 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount72,
    ISNULL(SUM(CASE WHEN wd.RowNum = 73 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount73,
    ISNULL(SUM(CASE WHEN wd.RowNum = 74 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount74,
    ISNULL(SUM(CASE WHEN wd.RowNum = 75 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount75,
    ISNULL(SUM(CASE WHEN wd.RowNum = 76 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount76,
    ISNULL(SUM(CASE WHEN wd.RowNum = 77 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount77,
    ISNULL(SUM(CASE WHEN wd.RowNum = 78 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount78,
    ISNULL(SUM(CASE WHEN wd.RowNum = 79 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount79,
    ISNULL(SUM(CASE WHEN wd.RowNum = 80 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount80,
    ISNULL(SUM(CASE WHEN wd.RowNum = 81 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount81,
    ISNULL(SUM(CASE WHEN wd.RowNum = 82 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount82,
    ISNULL(SUM(CASE WHEN wd.RowNum = 83 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount83,
    ISNULL(SUM(CASE WHEN wd.RowNum = 84 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount84,
    ISNULL(SUM(CASE WHEN wd.RowNum = 85 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount85,
    ISNULL(SUM(CASE WHEN wd.RowNum = 86 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount86,
    ISNULL(SUM(CASE WHEN wd.RowNum = 87 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount87,
    ISNULL(SUM(CASE WHEN wd.RowNum = 88 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount88,
    ISNULL(SUM(CASE WHEN wd.RowNum = 89 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount89,
    ISNULL(SUM(CASE WHEN wd.RowNum = 90 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount90
FROM 
    dbo.Customers c
LEFT JOIN 
    WeekDates wd ON 1 = 1  -- This gives all rows for each customer
LEFT JOIN 
    CustomerPayments cp ON c.CustomerID = cp.CustomerID AND cp.PaymentDate = wd.DateCreate
GROUP BY 
    c.CustomerID;


