create   view [dbo].[ViewCustomersPaymentsSingle]
AS
SELECT        CP.CustomerID, COALESCE (SUM(ATB.Amount * 1448), 0) AS AmountDenar
FROM            dbo.CustomersPayments AS CP LEFT OUTER JOIN
                         dbo.AddToBox AS ATB ON CP.CustomerPaymentID = ATB.CustomerPaymentID
GROUP BY CP.CustomerID

