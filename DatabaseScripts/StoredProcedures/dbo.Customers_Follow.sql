 
 
CREATE PROCEDURE [dbo].[Customers_Follow]
    @DelegateID INT,
    @PaymentDate DATETIME,
    @ShowType NVARCHAR(100)
AS
BEGIN
    SET NOCOUNT ON;

    DECLARE @PaymentDateOnly DATE = CAST(@PaymentDate AS DATE);

    ;WITH CustomersBase AS
    (
        SELECT
            C.CustomerID,
            C.DelegateID,
            C.UserID,
            C.CityID,
            C.CustomerName,
            C.Address,
            C.Longitude,
            C.Latitude,
            C.Notes,
            C.PhoneNumber,
            C.CustomerState,
            C.ShopName,
            C.StoreAddress,
            C.NearestFunctionPoint,
            C.StorePhoneNumber,
            C.Neighborhood,
            C.AmountReceverDay,
            C.AsyncID,
            C.AsyncState,
            C.SelectState,
            C.SaleName,
            C.IsLegal,
            C.IsFakeSale,
            C.ReceiptName,
            C.CustomerImage,
            C.CreatedDate,
            C.UpdatedDate
        FROM dbo.Customers C
        WHERE (@DelegateID = 0 OR C.DelegateID = @DelegateID)
    ),
    SalesData AS
    (
        SELECT 
            V.CustomerID,
            ROUND(COALESCE(SUM(V.AmountTotalSalesDenar), 0), -3) AS AmountTotalSales,
            ROUND(COALESCE(SUM(V.AmountTotalCostDenar), 0), -3) AS CostTotalSales,
            ROUND(COALESCE(SUM(V.AmountDaySalesDenar), 0), -3) AS AmountDaySales,
            MAX(V.DateCreate) AS DateSaleDevice
        FROM View_CustomersSalesDelegate V
        GROUP BY V.CustomerID
    ),
    PaymentData AS
    (
        SELECT 
            V.CustomerID,
            ROUND(COALESCE(SUM(V.AmountDenar), 0), -3) AS ReceiptsTotal
        FROM View_CustomersPaymentsDelegate V
        GROUP BY V.CustomerID
    ),
    AddToBoxData AS
    (
        SELECT 
            V.CustomerIDPayment AS CustomerID,
            COALESCE(SUM(V.Amount), 0) AS TotalAmount
        FROM View_AddToBox V
        GROUP BY V.CustomerIDPayment
    ),
    ItemsData AS
    (
        SELECT 
            V.CustomerID,
            STRING_AGG(CONCAT('(', V.ItemName, ' (', V.Quantity, ') )'), ' ') AS ItemsNames
        FROM View_SelectItemsSalesItemsNames V
        GROUP BY V.CustomerID
    ),
    TodayReceiptData AS
    (
        SELECT
            X.CustomerID,
            X.PaymentDate,
            ROUND(X.AmountDenar, -3) AS AmountDenar
        FROM
        (
            SELECT 
                V.CustomerID,
                V.PaymentDate,
                V.AmountDenar,
                ROW_NUMBER() OVER
                (
                    PARTITION BY V.CustomerID
                    ORDER BY V.PaymentDate DESC
                ) AS RN
            FROM View_ReceiptCustomerDate V
            WHERE V.PaymentDate >= @PaymentDateOnly
              AND V.PaymentDate < DATEADD(DAY, 1, @PaymentDateOnly)
        ) X
        WHERE X.RN = 1
    ),
    CustomerPaymentStats AS
    (
        SELECT
            CP.CustomerID,
            COUNT(*) AS CountReceiptDevice,
            MAX(CP.PaymentDate) AS LastPaymentDate
        FROM dbo.CustomersPayments CP
        GROUP BY CP.CustomerID
    ),
    CompanyData AS
    (
        SELECT TOP (1)
            PhoneNumber AS PhoneNumberCompany
        FROM dbo.CompanyInformation
    ),
    FinalData AS
    (
        SELECT
            C.CustomerID,
            C.DelegateID,
            C.UserID,
            C.CityID,
            C.CustomerName,
            C.Address,
            C.Longitude,
            C.Latitude,
            C.Notes,
            C.PhoneNumber,
            C.CustomerState,
            C.ShopName,
            C.StoreAddress,
            C.NearestFunctionPoint,
            C.StorePhoneNumber,
            C.Neighborhood,
            C.AmountReceverDay,
            C.AsyncID,
            C.AsyncState,
            C.SelectState,
            C.SaleName,
            C.IsLegal,
            C.IsFakeSale,
            C.ReceiptName,
            C.CustomerImage,
            C.CreatedDate,
            C.UpdatedDate,

            COALESCE(D.DelegateName, '') AS DelegateName,
            COALESCE(U.UserName, '') AS UserName,
            COALESCE(CI.CityName, '') AS CityName,

            SD.DateSaleDevice,
            COALESCE(SD.AmountTotalSales, 0) AS AmountTotalSales,
            COALESCE(SD.CostTotalSales, 0) AS CostTotalSales,
            COALESCE(SD.AmountDaySales, 0) AS AmountDaySales,
            COALESCE(PD.ReceiptsTotal, 0) AS ReceiptsTotal,

            ROUND(COALESCE(SD.AmountTotalSales, 0) - COALESCE(PD.ReceiptsTotal, 0), -3) AS AmountRemaining,

            FLOOR
            (
                CASE
                    WHEN SD.DateSaleDevice IS NULL THEN 0
                    WHEN ((DATEDIFF(DAY, SD.DateSaleDevice, GETDATE()) + 1) * COALESCE(NULLIF(SD.AmountDaySales, 0), 1)) = 0 THEN 0
                    ELSE
                        (
                            COALESCE(ATB.TotalAmount, 0) * 100.0
                            /
                            ((DATEDIFF(DAY, SD.DateSaleDevice, GETDATE()) + 1) * COALESCE(NULLIF(SD.AmountDaySales, 0), 1))
                        )
                END
            ) AS ReceiptRateDevice,

            CASE
                WHEN SD.DateSaleDevice IS NULL THEN 0
                ELSE DATEDIFF(DAY, SD.DateSaleDevice, GETDATE()) + 1
            END AS NumberOfDayDevice,

            CD.PhoneNumberCompany,

            COALESCE(CPS.CountReceiptDevice, 0) + 1 AS CountReceiptDevice,
            COALESCE(ID.ItemsNames, '') AS ItemsNames,
            COALESCE(CPS.LastPaymentDate, CONVERT(DATETIME, '1900-01-01')) AS LastPaymentDate,

            CASE
                WHEN CPS.LastPaymentDate IS NULL THEN 1
                ELSE DATEDIFF(DAY, CPS.LastPaymentDate, GETDATE()) + 1
            END AS NumberOfDayPayment,

            COALESCE(TRD.AmountDenar, 0) AS AmountReceipt
        FROM CustomersBase C
        LEFT JOIN dbo.Delegates D
            ON C.DelegateID = D.DelegateID
        LEFT JOIN dbo.Users U
            ON C.UserID = U.UserID
        LEFT JOIN dbo.Cities CI
            ON C.CityID = CI.CityID
        LEFT JOIN SalesData SD
            ON C.CustomerID = SD.CustomerID
        LEFT JOIN PaymentData PD
            ON C.CustomerID = PD.CustomerID
        LEFT JOIN AddToBoxData ATB
            ON C.CustomerID = ATB.CustomerID
        LEFT JOIN ItemsData ID
            ON C.CustomerID = ID.CustomerID
        LEFT JOIN CustomerPaymentStats CPS
            ON C.CustomerID = CPS.CustomerID
        LEFT JOIN TodayReceiptData TRD
            ON C.CustomerID = TRD.CustomerID
        CROSS JOIN CompanyData CD
    )
    SELECT *
    FROM FinalData
    WHERE
        ISNULL(IsFakeSale, 0) = 0
        AND (
            (
                @ShowType = N'المسددين'
                AND AmountReceipt > 0
            )
            OR
            (
                @ShowType <> N'المسددين'
                AND AmountReceipt = 0
            )
        )
    OPTION (RECOMPILE);
END

