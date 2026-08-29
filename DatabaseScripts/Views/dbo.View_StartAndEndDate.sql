create   view [dbo].[View_StartAndEndDate]
AS
WITH CustomerSalesData AS (SELECT        CustomerID, MIN(DateCreate) AS StartDate
                                                                  FROM            dbo.CustomersSales
                                                                  GROUP BY CustomerID), CustomerPaymentsData AS
    (SELECT        CustomerID, MAX(PaymentDate) AS EndDate
      FROM            dbo.CustomersPayments
      GROUP BY CustomerID), CustomerSalesAmount AS
    (SELECT        CustomerID, ISNULL(SUM(AmountTotalSalesDenar), 0) AS TotalSalesDenar
      FROM            dbo.View_CustomersSalesDelegate
      GROUP BY CustomerID), CustomerPaymentsAmount AS
    (SELECT        CustomerID, ROUND(ISNULL(SUM(AmountDenar), 0), - 3) AS TotalPaymentsDenar
      FROM            dbo.View_CustomersPaymentsDelegate
      GROUP BY CustomerID)
    SELECT        C.CustomerID, CSD.StartDate, CPD.EndDate
     FROM            dbo.Customers AS C LEFT OUTER JOIN
                              CustomerSalesData AS CSD ON C.CustomerID = CSD.CustomerID LEFT OUTER JOIN
                              CustomerPaymentsData AS CPD ON C.CustomerID = CPD.CustomerID LEFT OUTER JOIN
                              CustomerSalesAmount AS CSA ON C.CustomerID = CSA.CustomerID LEFT OUTER JOIN
                              CustomerPaymentsAmount AS CPA ON C.CustomerID = CPA.CustomerID
     WHERE        (ROUND(ISNULL(CSA.TotalSalesDenar - CPA.TotalPaymentsDenar, 0), - 3) = 0)

