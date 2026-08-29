create   view [dbo].[View_SelectItemCustomerAddRequestTemp]
AS
WITH CustomerAddRequestData AS (SELECT        CustomerAddRequestID, CustomerName
                                                                               FROM            dbo.CustomerAddRequest), ItemMerchantData AS
    (SELECT        ItemMerchantID, ItemMerchantName, Specifications, Notes
      FROM            dbo.ItemMerchant
      WHERE        (ItemMerchantState = 'true')), ItemImageData AS
    (SELECT        ItemMerchantID, Image
      FROM            dbo.View_ItemMerchant), ItemMerchantCustomerData AS
    (SELECT        ItemMerchantID, ISNULL(AmountCash, 0) AS AmountCash, ISNULL(AmountDay, 0) AS AmountDay, ISNULL(AmountWeek, 0) AS AmountWeek, ISNULL(AmountMonth, 0) AS AmountMonth
      FROM            dbo.View_ItemMerchantCustomer)
    SELECT        SICART.SelectItemCustomerAddRequestTempID, SICART.CustomerAddRequestID, SICART.ItemMerchantID, SICART.Quantity, SICART.AsyncID, SICART.AsyncState, SICART.HostHistoryID, CAR.CustomerName, 
                              IMD.ItemMerchantName, IID.Image, IMD.Specifications, IMD.Notes, IMCD.AmountCash * SICART.Quantity AS AmountCash, IMCD.AmountDay * SICART.Quantity AS AmountDay, 
                              IMCD.AmountWeek * SICART.Quantity AS AmountWeek, IMCD.AmountMonth * SICART.Quantity AS AmountMonth
     FROM            dbo.SelectItemCustomerAddRequestTemp AS SICART LEFT OUTER JOIN
                              CustomerAddRequestData AS CAR ON SICART.CustomerAddRequestID = CAR.CustomerAddRequestID LEFT OUTER JOIN
                              ItemMerchantData AS IMD ON SICART.ItemMerchantID = IMD.ItemMerchantID LEFT OUTER JOIN
                              ItemImageData AS IID ON SICART.ItemMerchantID = IID.ItemMerchantID LEFT OUTER JOIN
                              ItemMerchantCustomerData AS IMCD ON SICART.ItemMerchantID = IMCD.ItemMerchantID

