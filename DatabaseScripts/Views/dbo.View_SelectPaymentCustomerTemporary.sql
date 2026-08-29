create   view [dbo].[View_SelectPaymentCustomerTemporary]
AS
WITH DelegateData AS (SELECT        DelegateID, DelegateName
                                                    FROM            dbo.Delegates), CustomerData AS
    (SELECT        CustomerID, CustomerName
      FROM            dbo.Customers)
    SELECT        SPCT.SelectPaymentCustomerTemporaryID, SPCT.DelegateID, SPCT.CustomerID, SPCT.Amount, SPCT.AsyncID, SPCT.AsyncState, SPCT.Location, DD.DelegateName, CD.CustomerName
     FROM            dbo.SelectPaymentCustomerTemporary AS SPCT LEFT OUTER JOIN
                              DelegateData AS DD ON SPCT.DelegateID = DD.DelegateID LEFT OUTER JOIN
                              CustomerData AS CD ON SPCT.CustomerID = CD.CustomerID

