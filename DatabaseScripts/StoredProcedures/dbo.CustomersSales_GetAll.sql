CREATE proc [dbo].[CustomersSales_GetAll]
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL,
    @DelegateID INT = NULL,
    @CustomerName NVARCHAR(255) = NULL,
    @ItemName NVARCHAR(255) = NULL,
    @SaleName NVARCHAR(255) = NULL
AS
BEGIN
    SELECT * 
    FROM View_CustomersSales
    WHERE 
        (@FromDate IS NULL OR CONVERT(DATE, DateCreate) >= CONVERT(DATE, @FromDate))
        AND (@ToDate IS NULL OR CONVERT(DATE, DateCreate) <= CONVERT(DATE, @ToDate))
        AND (@DelegateID IS NULL OR DelegateID = @DelegateID)
        AND (@CustomerName IS NULL OR CustomerName LIKE N'%' + @CustomerName + N'%')
        AND (@ItemName IS NULL OR ItemsNames LIKE N'%' + @ItemName + N'%')
        AND (@SaleName IS NULL OR SaleName LIKE N'%' + @SaleName + N'%');
END


