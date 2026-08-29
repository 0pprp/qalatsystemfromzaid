CREATE PROCEDURE [dbo].[CustomersSales_GetByCustomerIDNew]
    @CustomerID INT = NULL,
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * 
    FROM View_CustomersSales
    WHERE 
        (@CustomerID IS NULL OR CustomerID = @CustomerID)
        AND (@FromDate IS NULL OR CONVERT(DATE, DateCreate) >= CONVERT(DATE, @FromDate))
        AND (@ToDate IS NULL OR CONVERT(DATE, DateCreate) <= CONVERT(DATE, @ToDate))
    ORDER BY DateCreate DESC;
END
