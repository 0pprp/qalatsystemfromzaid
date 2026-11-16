-- Canonical SP definition (keep in sync with Migrations/20261005_GetCustomersDelegateAll_ExplicitColumns.sql)
-- Includes debtors (AmountRemaining > 0) plus anyone who paid on the current Iraq business day
-- so settled-today customers remain printable with name / installment on the device.
-- Also includes customers parked on this delegate's linked «متوقفين» child list.
-- Those rows are returned under @DelegateID so the single-list delegate app can collect them.
CREATE OR ALTER PROC [dbo].[GetCustomersDelegateAll]
@DelegateID int
AS
BEGIN
  SET NOCOUNT ON;
  DECLARE @IraqBizDate date =
    CASE
      WHEN DATEPART(HOUR, DATEADD(HOUR,3,SYSUTCDATETIME())) < 3
        THEN DATEADD(DAY,-1,CONVERT(date,DATEADD(HOUR,3,SYSUTCDATETIME())))
      ELSE CONVERT(date,DATEADD(HOUR,3,SYSUTCDATETIME()))
    END;

  DECLARE @ListIds TABLE (DelegateID int PRIMARY KEY);
  INSERT INTO @ListIds (DelegateID) VALUES (@DelegateID);

  IF OBJECT_ID(N'dbo.SelectDelegate', N'U') IS NOT NULL
  BEGIN
    INSERT INTO @ListIds (DelegateID)
    SELECT DISTINCT sd.DelegateChildID
    FROM dbo.SelectDelegate sd
    INNER JOIN dbo.Delegates ch ON ch.DelegateID = sd.DelegateChildID
    WHERE sd.DelegateFatherID = @DelegateID
      AND sd.DelegateChildID IS NOT NULL
      AND sd.DelegateChildID <> @DelegateID
      AND ISNULL(ch.DelegateName, N'') LIKE N'%متوقفين%'
      AND NOT EXISTS (SELECT 1 FROM @ListIds x WHERE x.DelegateID = sd.DelegateChildID);
  END

  SELECT
    vc.CustomerID,
    @DelegateID AS DelegateID,
    vc.CityID,
    vc.CustomerName,
    vc.Address,
    vc.PhoneNumber,
    vc.ShopName,
    vc.IsLegal,
    vc.PhoneNumberCompany,
    vc.DateSaleDevice,
    vc.AmountTotalSales,
    vc.AmountDaySales,
    vc.ReceiptsTotal,
    vc.AmountRemaining,
    vc.LastPaymentDate,
    vc.CountReceiptDevice,
    vc.ItemsNames,
    vc.Amount1, vc.Amount2, vc.Amount3, vc.Amount4, vc.Amount5, vc.Amount6, vc.Amount7,
    vc.NumberOfDayPayment
  FROM dbo.View_Customers vc
  WHERE vc.DelegateID IN (SELECT DelegateID FROM @ListIds)
    AND (
      ISNULL(vc.AmountRemaining, 0) > 0
      OR EXISTS (
        SELECT 1
        FROM dbo.CustomersPayments cp
        WHERE cp.CustomerID = vc.CustomerID
          AND CASE
            WHEN DATEPART(HOUR, DATEADD(HOUR,3,cp.PaymentDate)) < 3
              THEN DATEADD(DAY,-1,CONVERT(date,DATEADD(HOUR,3,cp.PaymentDate)))
            ELSE CONVERT(date,DATEADD(HOUR,3,cp.PaymentDate))
          END = @IraqBizDate
      )
    );
END
GO
