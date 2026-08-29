create   view [dbo].[View_PaymentDeviceWeek]
AS
SELECT        CustomerID, DelegateID, UserID, CityID, CustomerName, Address, Longitude, Latitude, CustomerImage, Notes, PhoneNumber, CustomerState, ShopName, StoreAddress, NearestFunctionPoint, StorePhoneNumber, 
                         Neighborhood, AmountReceverDay, AsyncState, AsyncID, SelectState, DelegateName, UserName, CityName, AmountTotalSales, CostTotalSales, AmountDaySales, ReceiptsTotal, AmountRemaining, ItemsNames,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 1) AS date))) AS Amount1,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_6
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 2) AS date))) AS Amount2,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_5
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 3) AS date))) AS Amount3,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_4
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 4) AS date))) AS Amount4,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_3
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 5) AS date))) AS Amount5,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_2
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 6) AS date))) AS Amount6,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 7) AS date))) AS Amount7
FROM            dbo.View_Customers

