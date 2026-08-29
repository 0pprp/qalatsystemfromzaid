create   view [dbo].[View_CustomersPaymentsRequest]
AS
WITH CustomerData AS (SELECT        CustomerID, CustomerName
                                                     FROM            dbo.Customers), DelegateData AS
    (SELECT        DelegateID, DelegateName
      FROM            dbo.Delegates)
    SELECT        dbo.CustomersPaymentsRequest.CustomersPaymentsRequestID, dbo.CustomersPaymentsRequest.CustomerID, dbo.CustomersPaymentsRequest.PaymentDate, dbo.CustomersPaymentsRequest.BoundNumber, 
                              dbo.CustomersPaymentsRequest.DelegateID, dbo.CustomersPaymentsRequest.AccountZero, dbo.CustomersPaymentsRequest.DelegateState, dbo.CustomersPaymentsRequest.Amount, 
                              dbo.CustomersPaymentsRequest.AsyncState, dbo.CustomersPaymentsRequest.AsyncID, dbo.CustomersPaymentsRequest.SelectState, dbo.CustomersPaymentsRequest.Location, CustomerData_1.CustomerName, 
                              DelegateData_1.DelegateName
     FROM            dbo.CustomersPaymentsRequest LEFT OUTER JOIN
                              CustomerData AS CustomerData_1 ON dbo.CustomersPaymentsRequest.CustomerID = CustomerData_1.CustomerID LEFT OUTER JOIN
                              DelegateData AS DelegateData_1 ON dbo.CustomersPaymentsRequest.DelegateID = DelegateData_1.DelegateID

