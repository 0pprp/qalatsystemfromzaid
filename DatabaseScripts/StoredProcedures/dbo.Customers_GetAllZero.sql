CREATE proc [dbo].[Customers_GetAllZero]
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL
AS
BEGIN
    SELECT * 
    FROM View_CustomersDelegate
    WHERE 
        (@FromDate IS NULL OR CONVERT(DATE, LastPaymentDate) >= CONVERT(DATE, @FromDate))
        AND (@ToDate IS NULL OR CONVERT(DATE, LastPaymentDate) <= CONVERT(DATE, @ToDate))
        AND AmountRemaining = 0;   
END


