CREATE proc [dbo].[GetAllCustomerCity]
@CityId int
as
SELECT        CustomerID,CustomerName,DelegateID,PhoneNumber,Address,
                             (SELECT        DelegateName
                               FROM            dbo.Delegates
                               WHERE        (DelegateID = dbo.Customers.DelegateID)) AS DelegateName,
                             (SELECT        TOP 1 DateCreate
                               FROM            View_CustomersSalesDelegate
                               WHERE        CustomerID = Customers.CustomerID) AS DateSaleDevice,
                             (SELECT        ROUND(ISNULL(sum(AmountTotalSalesDenar), 0), - 3)
                               FROM            View_CustomersSalesDelegate
                               WHERE        CustomerID = Customers.CustomerID) AS AmountTotalSales,
                             (SELECT        ROUND(ISNULL(sum(AmountDaySalesDenar), 0), - 3)
                               FROM            View_CustomersSalesDelegate
                               WHERE        CustomerID = Customers.CustomerID) AS AmountDaySales,
                             (SELECT        ROUND(ISNULL(sum(AmountDenar), 0), - 3)
                               FROM            View_CustomersPaymentsDelegate
                               WHERE        CustomerID = Customers.CustomerID) AS ReceiptsTotal, (ROUND(ISNULL
                             ((SELECT        ISNULL(sum(AmountTotalSalesDenar), 0)
                                 FROM            View_CustomersSalesDelegate
                                 WHERE        CustomerID = Customers.CustomerID) -
                             (SELECT        ROUND(ISNULL(sum(AmountDenar), 0), - 3)
                               FROM            View_CustomersPaymentsDelegate
                               WHERE        CustomerID = Customers.CustomerID), 0), - 3)) AS AmountRemaining ,
                             (SELECT        + ' ( ' + '' + ItemName + '' + ' ( ' + CAST(Quantity AS nvarchar(255)) + ' ) ' + ' ) '
                               FROM            View_SelectItemsSalesItemsNames
                               WHERE        CustomerID = dbo.Customers.CustomerID FOR XML PATH('')) AS ItemsNames 
FROM            dbo.Customers

