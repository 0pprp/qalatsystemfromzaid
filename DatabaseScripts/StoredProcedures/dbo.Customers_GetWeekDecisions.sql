CREATE OR ALTER PROC [dbo].[Customers_GetWeekDecisions]
    @DecisionType NVARCHAR(50) = NULL,
    @FromDate DATETIME = NULL,
    @ToDate DATETIME = NULL,
    @CustomerID INT = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT
        D.DecisionID,
        D.CustomerID,
        D.UserID,
        D.DecisionType,
        D.WeekPaid,
        D.AmountTotalSales,
        D.PaidPercent,
        D.WeekStartDate,
        D.WeekEndDate,
        D.SnoozeUntil,
        D.Note,
        D.CreatedDate,
        C.CustomerName,
        C.PhoneNumber,
        U.UserName,
        U.UserType,
        ISNULL(C.IsLegal, 0) AS IsLegal,
        ISNULL(C.IsFakeSale, 0) AS IsFakeSale
    FROM CustomerWeekDecisions D
    INNER JOIN Customers C ON C.CustomerID = D.CustomerID
    INNER JOIN Users U ON U.UserID = D.UserID
    WHERE (@DecisionType IS NULL OR @DecisionType = N'' OR @DecisionType = N'الكل' OR D.DecisionType = @DecisionType)
      AND (@FromDate IS NULL OR D.CreatedDate >= @FromDate)
      AND (@ToDate IS NULL OR D.CreatedDate < DATEADD(DAY, 1, @ToDate))
      AND (@CustomerID IS NULL OR D.CustomerID = @CustomerID)
    ORDER BY D.CreatedDate DESC;
END
