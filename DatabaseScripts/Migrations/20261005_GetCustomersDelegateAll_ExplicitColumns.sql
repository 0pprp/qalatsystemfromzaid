-- GetCustomersDelegateAll: explicit columns used by delegate mobile apps (no SELECT *).
-- Apply on isolated branch DB first. Measure logical reads before claiming improvement.
CREATE OR ALTER PROC [dbo].[GetCustomersDelegateAll]
@DelegateID int
AS
BEGIN
  SET NOCOUNT ON;
  SELECT
    CustomerID,
    DelegateID,
    CityID,
    CustomerName,
    Address,
    PhoneNumber,
    ShopName,
    IsLegal,
    PhoneNumberCompany,
    DateSaleDevice,
    AmountTotalSales,
    AmountDaySales,
    ReceiptsTotal,
    AmountRemaining,
    LastPaymentDate,
    CountReceiptDevice,
    ItemsNames,
    Amount1, Amount2, Amount3, Amount4, Amount5, Amount6, Amount7,
    NumberOfDayPayment
  FROM dbo.View_Customers vc
  WHERE vc.DelegateID = @DelegateID
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
          END = CASE
            WHEN DATEPART(HOUR, DATEADD(HOUR,3,SYSUTCDATETIME())) < 3
              THEN DATEADD(DAY,-1,CONVERT(date,DATEADD(HOUR,3,SYSUTCDATETIME())))
            ELSE CONVERT(date,DATEADD(HOUR,3,SYSUTCDATETIME()))
          END
      )
    );
END
GO

