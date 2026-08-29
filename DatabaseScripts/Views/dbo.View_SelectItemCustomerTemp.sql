create   view [dbo].[View_SelectItemCustomerTemp]
AS
SELECT        SelectItemCustomerTempID, CustomerID, ItemMerchantID, Quantity, AsyncID, AsyncState,
                             (SELECT        CustomerName
                               FROM            dbo.Customers
                               WHERE        (CustomerID = dbo.SelectItemCustomerTemp.CustomerID)) AS CustomerName,
                             (SELECT        ItemMerchantName
                               FROM            dbo.ItemMerchant
                               WHERE        (ItemMerchantID = dbo.SelectItemCustomerTemp.ItemMerchantID)) AS ItemMerchantName,
                             (SELECT        Specifications
                               FROM            dbo.ItemMerchant AS ItemMerchant_2
                               WHERE        (ItemMerchantID = dbo.SelectItemCustomerTemp.ItemMerchantID)) AS Specifications,
                             (SELECT        Notes
                               FROM            dbo.ItemMerchant AS ItemMerchant_1
                               WHERE        (ItemMerchantID = dbo.SelectItemCustomerTemp.ItemMerchantID)) AS Notes,
                             (SELECT        Image
                               FROM            dbo.View_ItemMerchant
                               WHERE        (ItemMerchantID = dbo.SelectItemCustomerTemp.ItemMerchantID)) AS Image,
                             (SELECT        ISNULL(AmountCash * dbo.SelectItemCustomerTemp.Quantity, 0) AS Expr1
                               FROM            dbo.View_ItemMerchantCustomer
                               WHERE        (ItemMerchantID = dbo.SelectItemCustomerTemp.ItemMerchantID)) AS AmountCash,
                             (SELECT        ISNULL(AmountDay * dbo.SelectItemCustomerTemp.Quantity, 0) AS Expr1
                               FROM            dbo.View_ItemMerchantCustomer AS View_ItemMerchantCustomer_3
                               WHERE        (ItemMerchantID = dbo.SelectItemCustomerTemp.ItemMerchantID)) AS AmountDay,
                             (SELECT        ISNULL(AmountWeek * dbo.SelectItemCustomerTemp.Quantity, 0) AS Expr1
                               FROM            dbo.View_ItemMerchantCustomer AS View_ItemMerchantCustomer_2
                               WHERE        (ItemMerchantID = dbo.SelectItemCustomerTemp.ItemMerchantID)) AS AmountWeek,
                             (SELECT        ISNULL(AmountMonth * dbo.SelectItemCustomerTemp.Quantity, 0) AS Expr1
                               FROM            dbo.View_ItemMerchantCustomer AS View_ItemMerchantCustomer_1
                               WHERE        (ItemMerchantID = dbo.SelectItemCustomerTemp.ItemMerchantID)) AS AmountMonth
FROM            dbo.SelectItemCustomerTemp

