CREATE proc [dbo].[GetCurrentReceiptDelegate]
@DelegateID  int = NULL
as
select 
SelectPaymentCustomerTemporaryID,
CustomerID,
(select CustomerName from Customers where CustomerID=SelectPaymentCustomerTemporary.CustomerID) as CustomerName ,
(select PhoneNumber from Customers where CustomerID=SelectPaymentCustomerTemporary.CustomerID) as PhoneNumber,
(select Address from Customers where CustomerID=SelectPaymentCustomerTemporary.CustomerID) as Address, 
(select ShopName from Customers where CustomerID=SelectPaymentCustomerTemporary.CustomerID) as ShopName,
DelegateID,
                             (SELECT        DelegateName
                               FROM            dbo.Delegates
                               WHERE        (DelegateID = dbo.SelectPaymentCustomerTemporary.DelegateID)) AS DelegateName,
 (SELECT        TOP 1 DateCreate
                               FROM            CustomersSales
                               WHERE        CustomerID = SelectPaymentCustomerTemporary.CustomerID) AS DateSaleDevice,
                             (SELECT        ISNULL(sum(AmountTotalSalesDenar), 0)
                               FROM            View_CustomersSales
                               WHERE        CustomerID = SelectPaymentCustomerTemporary.CustomerID) AS AmountTotalSales,
                             (SELECT        ISNULL(sum(AmountDaySalesDenar), 0)
                               FROM            View_CustomersSales
                               WHERE        CustomerID = SelectPaymentCustomerTemporary.CustomerID) AS AmountDaySales,
                             (SELECT        ISNULL(SUM(AmountDenar), 0)
                               FROM            dbo.View_AddToBox
                               WHERE        (CustomerIDPayment = dbo.SelectPaymentCustomerTemporary.CustomerID)) AS ReceiptsTotal, (ROUND(ISNULL
                             ((SELECT        ISNULL(sum(AmountTotalSalesDenar), 0)
                                 FROM            View_CustomersSales
                                 WHERE        CustomerID = SelectPaymentCustomerTemporary.CustomerID) -
                             (SELECT        ISNULL(SUM(AmountDenar), 0)
                               FROM            dbo.View_AddToBox
                               WHERE        (CustomerIDPayment = dbo.SelectPaymentCustomerTemporary.CustomerID)), 0), - 3)) AS AmountRemaining,
							                                (SELECT        + ' ( ' + '' + ItemName + '' + ' ( ' + CAST(Quantity AS nvarchar(255)) + ' ) ' + ' ) '
                               FROM            View_SelectItemsSales
                               WHERE        CustomerID = dbo.SelectPaymentCustomerTemporary.CustomerID FOR XML PATH('')) AS ItemsNames,
							   Amount,
							   Location
from SelectPaymentCustomerTemporary
where DelegateID=@DelegateID

