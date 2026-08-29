create   view [dbo].[View_Week]
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
    ISNULL(SUM(CASE WHEN wd.RowNum = 7 THEN cp.TotalAmount ELSE 0 END), 0) AS Amount7
FROM 
    dbo.Customers c
LEFT JOIN 
    WeekDates wd ON 1 = 1  -- This gives all rows for each customer
LEFT JOIN 
    CustomerPayments cp ON c.CustomerID = cp.CustomerID AND cp.PaymentDate = wd.DateCreate
GROUP BY 
    c.CustomerID;


