CREATE OR ALTER PROC [dbo].[GetCustomersByDelegatePermissions]
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

  SELECT * FROM dbo.View_Customers vc
  WHERE EXISTS (
      SELECT 1 FROM dbo.SelectDelegate sd
      WHERE sd.DelegateChildID = vc.DelegateID
        AND sd.DelegateFatherID = @DelegateID
    )
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
