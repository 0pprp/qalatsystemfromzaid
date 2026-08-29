create   view [dbo].[View_PaymentDeviceMonth]
AS
SELECT        CustomerID, DelegateID, UserID, CityID, CustomerName, Address, Longitude, Latitude, CustomerImage, Notes, PhoneNumber, CustomerState, ShopName, StoreAddress, NearestFunctionPoint, StorePhoneNumber, 
                         Neighborhood, AmountReceverDay, AsyncState, AsyncID, SelectState, DelegateName, UserName, CityName, AmountTotalSales, CostTotalSales, AmountDaySales, ReceiptsTotal, AmountRemaining, ItemsNames,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 1) AS date))) AS Amount1,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_29
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 2) AS date))) AS Amount2,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_28
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 3) AS date))) AS Amount3,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_27
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 4) AS date))) AS Amount4,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_26
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 5) AS date))) AS Amount5,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_25
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 6) AS date))) AS Amount6,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_24
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 7) AS date))) AS Amount7,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_23
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 8) AS date))) AS Amount8,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM  dbo.View_CustomersPayments AS View_CustomersPayments_22
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 9) AS date))) AS Amount9,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_21
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 10) AS date))) AS Amount10,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_20
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 11) AS date))) AS Amount11,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_19
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 12) AS date))) AS Amount12,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_18
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 13) AS date))) AS Amount13,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_17
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 14) AS date))) AS Amount14,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_16
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 15) AS date))) AS Amount15,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_15
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 16) AS date))) AS Amount16,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_14
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 17) AS date))) AS Amount17,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_13
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 18) AS date))) AS Amount18,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_12
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 19) AS date))) AS Amount19,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_11
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 20) AS date))) AS Amount20,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_10
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 21) AS date))) AS Amount21,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_9
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 22) AS date))) AS Amount22,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_8
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 23) AS date))) AS Amount23,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_7
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 24) AS date))) AS Amount24,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_6
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 25) AS date))) AS Amount25,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_5
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 26) AS date))) AS Amount26,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_4
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 27) AS date))) AS Amount27,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_3
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 28) AS date))) AS Amount28,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_2
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 29) AS date))) AS Amount29,
                             (SELECT        ISNULL(SUM(AmountDenar), 0) AS Expr1
                               FROM            dbo.View_CustomersPayments AS View_CustomersPayments_1
                               WHERE        (CustomerID = dbo.View_Customers.CustomerID) AND (PaymentDate =
                                                             (SELECT        CONVERT(date, GETUTCDATE() - 30) AS date))) AS Amount30
FROM            dbo.View_Customers

