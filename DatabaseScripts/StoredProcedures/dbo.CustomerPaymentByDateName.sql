CREATE proc [dbo].[CustomerPaymentByDateName]
@CustomerName nvarchar(255),
@PaymentDate datetime
as
select 
CustomerID,
CustomerName,
PhoneNumber,
Address, 
ShopName,
DelegateID,
                             (SELECT        DelegateName
                               FROM            dbo.Delegates
                               WHERE        (DelegateID = dbo.Customers.DelegateID)) AS DelegateName,
 (SELECT        TOP 1 DateCreate
                               FROM            CustomersSales
                               WHERE        CustomerID = Customers.CustomerID) AS DateSaleDevice,
                             (SELECT        ISNULL(sum(AmountTotalSalesDenar), 0)
                               FROM            View_CustomersSales
                               WHERE        CustomerID = Customers.CustomerID) AS AmountTotalSales,
                             (SELECT        ISNULL(sum(AmountDaySalesDenar), 0)
                               FROM            View_CustomersSales
                               WHERE        CustomerID = Customers.CustomerID) AS AmountDaySales,
                             (SELECT        ISNULL(SUM(AmountDenar), 0)
                               FROM            dbo.View_AddToBox
                               WHERE        (CustomerIDPayment = dbo.Customers.CustomerID)) AS ReceiptsTotal, (ROUND(ISNULL
                             ((SELECT        ISNULL(sum(AmountTotalSalesDenar), 0)
                                 FROM            View_CustomersSales
                                 WHERE        CustomerID = Customers.CustomerID) -
                             (SELECT        ISNULL(SUM(AmountDenar), 0)
                               FROM            dbo.View_AddToBox
                               WHERE        (CustomerIDPayment = dbo.Customers.CustomerID)), 0), - 3)) AS AmountRemaining,
							                                (SELECT        + ' ( ' + '' + ItemName + '' + ' ( ' + CAST(Quantity AS nvarchar(255)) + ' ) ' + ' ) '
                               FROM            View_SelectItemsSales
                               WHERE        CustomerID = dbo.Customers.CustomerID FOR XML PATH('')) AS ItemsNames,
							   (select ISNULL(sum(AmountDenar),0) from View_CustomersPayments where CustomerID=Customers.CustomerID and CONVERT(date, PaymentDate)=@PaymentDate)as Amount,
							   (select top 1 Location from View_CustomersPayments where CustomerID=Customers.CustomerID and CONVERT(date, PaymentDate)=@PaymentDate)as Location
from Customers
where CustomerName like N'%'+@CustomerName+N'%'

