create   view [dbo].[View_LastPayment]
AS
WITH LastPaymentData AS (SELECT        CustomerID, MAX(PaymentDate) AS LastPayment
                                                             FROM            dbo.CustomersPayments
                                                             GROUP BY CustomerID)
    SELECT        dbo.Customers.CustomerID, ISNULL(LastPaymentData_1.LastPayment, NULL) AS LastPayment
     FROM            dbo.Customers LEFT OUTER JOIN
                              LastPaymentData AS LastPaymentData_1 ON dbo.Customers.CustomerID = LastPaymentData_1.CustomerID

