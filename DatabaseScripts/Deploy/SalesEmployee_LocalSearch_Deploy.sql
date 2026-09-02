-- Local lab only: DatabaseCompany in Docker is missing ERP views/SPs that search needs.
-- This is a thin stand-in so موظف المبيعات can search customers without the full company schema.

CREATE OR ALTER VIEW [dbo].[View_CustomersDelegate]
AS
SELECT
    C.CustomerID,
    C.DelegateID,
    C.UserID,
    C.CityID,
    C.CustomerName,
    C.Address,
    C.Longitude,
    C.Latitude,
    C.CustomerImage,
    C.Notes,
    C.PhoneNumber,
    C.CustomerState,
    C.ShopName,
    C.StoreAddress,
    C.NearestFunctionPoint,
    C.StorePhoneNumber,
    C.Neighborhood,
    C.AmountReceverDay,
    C.AsyncState,
    C.AsyncID,
    C.SelectState,
    C.SaleName,
    C.ReceiptName,
    C.IsLegal,
    C.IsFakeSale,
    C.CreatedDate,
    C.UpdatedDate,
    CAST(N'محلي' AS nvarchar(100)) AS CityName,
    D.DelegateName,
    CAST(NULL AS datetime) AS DateSaleDevice,
    CAST(NULL AS datetime) AS LastPaymentDate,
    CAST(0 AS float) AS AmountRemaining
FROM dbo.Customers C
LEFT JOIN dbo.Delegates D ON C.DelegateID = D.DelegateID;
GO

CREATE OR ALTER PROC [dbo].[Customers_GetAll]
    @DelegateID INT = NULL,
    @TextSearch NVARCHAR(255) = NULL,
    @ShowType NVARCHAR(50) = NULL
AS
BEGIN
    SET NOCOUNT ON;

    SELECT * FROM View_CustomersDelegate
    WHERE
        (@DelegateID IS NULL OR DelegateID = @DelegateID)
        AND (@TextSearch IS NULL
             OR CustomerName LIKE N'%' + @TextSearch + N'%'
             OR PhoneNumber LIKE N'%' + @TextSearch + N'%')
        AND CustomerState = 1
        AND (
            (@ShowType = N'الجميع' AND AmountRemaining >= 0)
            OR (@ShowType = N'الغير مصفرين' AND AmountRemaining > 0)
            OR (@ShowType = N'المصفرين' AND AmountRemaining = 0)
            OR (@ShowType = N'القانونية' AND AmountRemaining > 0 AND IsLegal = 'true')
            OR (@ShowType = N'المستمرين' AND AmountRemaining > 0
                AND ISNULL(IsLegal, 0) = 0
                AND (LastPaymentDate > DATEADD(YEAR, -1, GETDATE()) OR DateSaleDevice > DATEADD(YEAR, -1, GETDATE())))
            OR (@ShowType = N'المتوقفين' AND AmountRemaining > 0
                AND ISNULL(IsLegal, 0) = 0
                AND (ISNULL(LastPaymentDate, '1900-01-01') <= DATEADD(YEAR, -1, GETDATE())
                     AND ISNULL(DateSaleDevice, '1900-01-01') <= DATEADD(YEAR, -1, GETDATE())))
        );
END
GO
