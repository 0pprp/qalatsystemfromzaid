create   view [dbo].[View_CustomersPaymentsAndRemaining]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), CustomerData AS
    (SELECT        CustomerID, CustomerName, PhoneNumber, CityID
      FROM            dbo.Customers), CityData AS
    (SELECT        CityID, CityName
      FROM            dbo.Cities), BoxData AS
    (SELECT        BoxID, BoxName
      FROM            dbo.Boxes), DelegateData AS
    (SELECT        DelegateID, DelegateName
      FROM            dbo.Delegates), AmountData AS
    (SELECT        CustomerPaymentID, ISNULL(SUM(Amount * 1448), 0) AS AmountDenar
      FROM            dbo.AddToBox
      GROUP BY CustomerPaymentID), SalesData AS
    (SELECT        CustomerID, ISNULL(SUM(AmountTotalSalesDenar), 0) AS TotalSales
      FROM            dbo.View_CustomersSales
      GROUP BY CustomerID), PaymentsData AS
    (SELECT        CustomerIDPayment, ISNULL(SUM(AmountDenar), 0) AS TotalPayments
      FROM            dbo.View_AddToBox
      GROUP BY CustomerIDPayment)
    SELECT        dbo.CustomersPayments.CustomerPaymentID, dbo.CustomersPayments.UserID, dbo.CustomersPayments.CustomerID, dbo.CustomersPayments.BoxID, dbo.CustomersPayments.PaymentDate, 
                              dbo.CustomersPayments.BoundNumber, dbo.CustomersPayments.DelegateID, dbo.CustomersPayments.AccountZero, dbo.CustomersPayments.DelegateState, dbo.CustomersPayments.AsyncState, 
                              dbo.CustomersPayments.AsyncID, dbo.CustomersPayments.SelectState, dbo.CustomersPayments.Location, UserData_1.UserName, CustomerData_1.CustomerName, CustomerData_1.PhoneNumber, BoxData_1.BoxName, 
                              DelegateData_1.DelegateName, AmountData_1.AmountDenar, CustomerData_1.CityID, CityData_1.CityName, ROUND(ISNULL(SalesData_1.TotalSales, 0) - ISNULL(PaymentsData_1.TotalPayments, 0), - 3) 
                              AS AmountRemaining
     FROM            dbo.CustomersPayments LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.CustomersPayments.UserID = UserData_1.UserID LEFT OUTER JOIN
                              CustomerData AS CustomerData_1 ON dbo.CustomersPayments.CustomerID = CustomerData_1.CustomerID LEFT OUTER JOIN
                              CityData AS CityData_1 ON CustomerData_1.CityID = CityData_1.CityID LEFT OUTER JOIN
                              BoxData AS BoxData_1 ON dbo.CustomersPayments.BoxID = BoxData_1.BoxID LEFT OUTER JOIN
                              DelegateData AS DelegateData_1 ON dbo.CustomersPayments.DelegateID = DelegateData_1.DelegateID LEFT OUTER JOIN
                              AmountData AS AmountData_1 ON dbo.CustomersPayments.CustomerPaymentID = AmountData_1.CustomerPaymentID LEFT OUTER JOIN
                              SalesData AS SalesData_1 ON dbo.CustomersPayments.CustomerID = SalesData_1.CustomerID LEFT OUTER JOIN
                              PaymentsData AS PaymentsData_1 ON dbo.CustomersPayments.CustomerID = PaymentsData_1.CustomerIDPayment

