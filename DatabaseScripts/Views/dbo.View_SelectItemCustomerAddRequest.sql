create   view [dbo].[View_SelectItemCustomerAddRequest]
AS
WITH CustomerAddRequestData AS (SELECT        CustomerAddRequestID, CustomerName
                                                                               FROM            dbo.CustomerAddRequest), ItemMerchantData AS
    (SELECT        ItemMerchantID, ItemMerchantName
      FROM            dbo.ItemMerchant), ItemImageData AS
    (SELECT        ItemMerchantID, Image
      FROM            dbo.View_ItemMerchant)
    SELECT        SICAR.SelectItemCustomerAddRequestID, SICAR.CustomerAddRequestID, SICAR.ItemMerchantID, SICAR.Quantity, SICAR.AsyncID, SICAR.AsyncState, CAR.CustomerName, IMD.ItemMerchantName, IID.Image
     FROM            dbo.SelectItemCustomerAddRequest AS SICAR LEFT OUTER JOIN
                              CustomerAddRequestData AS CAR ON SICAR.CustomerAddRequestID = CAR.CustomerAddRequestID LEFT OUTER JOIN
                              ItemMerchantData AS IMD ON SICAR.ItemMerchantID = IMD.ItemMerchantID LEFT OUTER JOIN
                              ItemImageData AS IID ON SICAR.ItemMerchantID = IID.ItemMerchantID

