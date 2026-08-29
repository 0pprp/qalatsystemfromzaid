CREATE proc [dbo].[Buys_GetByDateByTextSearch]
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL,
    @TextSearch NVARCHAR(255) = NULL
AS
BEGIN
    SELECT * 
    FROM View_Buys 
    WHERE 
        (@FromDate IS NULL OR CONVERT(DATE, DateCreate) >= CONVERT(DATE, @FromDate))
        AND (@ToDate IS NULL OR CONVERT(DATE, DateCreate) <= CONVERT(DATE, @ToDate))
        AND (@TextSearch IS NULL 
             OR BuyID LIKE N'%' + @TextSearch + N'%' 
             OR SupplierName LIKE N'%' + @TextSearch + N'%'
             OR ItemsNames LIKE N'%' + @TextSearch + N'%'
             OR Notes LIKE N'%' + @TextSearch + N'%');
END


