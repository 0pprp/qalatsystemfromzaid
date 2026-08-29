create   view [dbo].[View_SelectItemCustomer]
AS
WITH CustomerOldRequestData AS (SELECT        CustomerOldRequestID, CustomerID
                                                                               FROM            dbo.CustomerOldRequest), CustomerData AS
    (SELECT        CustomerID, CustomerName
      FROM            dbo.Customers), ItemMerchantData AS
    (SELECT        ItemMerchantID, ItemMerchantName, ItemMerchantState
      FROM            dbo.ItemMerchant
      WHERE        (ItemMerchantState = 'true')), ItemImageData AS
    (SELECT        ItemMerchantID, Image
      FROM            dbo.View_ItemMerchant)
    SELECT        SIC.SelectItemCustomerAddRequestID, SIC.ItemMerchantID, SIC.Quantity, SIC.AsyncID, SIC.AsyncState, SIC.CustomerOldRequestID, COR.CustomerID, CD.CustomerName, IMD.ItemMerchantName, IID.Image
     FROM            dbo.SelectItemCustomer AS SIC LEFT OUTER JOIN
                              CustomerOldRequestData AS COR ON SIC.CustomerOldRequestID = COR.CustomerOldRequestID LEFT OUTER JOIN
                              CustomerData AS CD ON COR.CustomerID = CD.CustomerID LEFT OUTER JOIN
                              ItemMerchantData AS IMD ON SIC.ItemMerchantID = IMD.ItemMerchantID LEFT OUTER JOIN
                              ItemImageData AS IID ON SIC.ItemMerchantID = IID.ItemMerchantID

