create   view [dbo].[View_Month]
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
    ISNULL(SUM(CASE WHEN wd.RowNum = 30 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount30
FROM 
    dbo.Customers c
LEFT JOIN 
    WeekDates wd ON 1 = 1  -- This gives all rows for each customer
LEFT JOIN 
    CustomerPayments cp ON c.CustomerID = cp.CustomerID AND cp.PaymentDate = wd.DateCreate
GROUP BY 
    c.CustomerID;


