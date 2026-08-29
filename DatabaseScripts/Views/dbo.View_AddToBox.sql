create   view [dbo].[View_AddToBox]
AS
SELECT        dbo.AddToBox.AddToBoxID, dbo.AddToBox.BoxID, dbo.AddToBox.Amount, dbo.AddToBox.Notes, dbo.AddToBox.UserID, dbo.AddToBox.SupplierID, dbo.AddToBox.DelegateID, dbo.AddToBox.DateCreate, 
                         dbo.AddToBox.DateModify, dbo.AddToBox.CustomerPaymentID, dbo.AddToBox.EmployeeID, dbo.AddToBox.DocumentID, dbo.AddToBox.CustomerID, dbo.AddToBox.TransferBoxID, dbo.AddToBox.AsyncState, 
                         dbo.AddToBox.AsyncID, dbo.AddToBox.CustomerPaymentBalanceID, dbo.CustomersPayments.CustomerID AS CustomerIDPayment, dbo.CustomersPayments.AccountZero AS AccountZero, dbo.Boxes.BoxName AS BoxName, 
                         ISNULL(dbo.AddToBox.Amount * 1448, 0) AS AmountDenar, dbo.Users.UserName AS UserName, dbo.Suppliers.SupplierName AS SupplierName, dbo.Delegates.DelegateName AS DelegateName, 
                         dbo.Employees.EmployeeName AS EmployeeName, dbo.Customers.CustomerName AS CustomerName, Customers_1.CustomerName AS CustomerNamePayment
FROM            dbo.AddToBox LEFT OUTER JOIN
                         dbo.CustomersPayments ON dbo.AddToBox.CustomerPaymentID = dbo.CustomersPayments.CustomerPaymentID LEFT OUTER JOIN
                         dbo.Boxes ON dbo.AddToBox.BoxID = dbo.Boxes.BoxID LEFT OUTER JOIN
                         dbo.Users ON dbo.AddToBox.UserID = dbo.Users.UserID LEFT OUTER JOIN
                         dbo.Suppliers ON dbo.AddToBox.SupplierID = dbo.Suppliers.SupplierID LEFT OUTER JOIN
                         dbo.Delegates ON dbo.AddToBox.DelegateID = dbo.Delegates.DelegateID LEFT OUTER JOIN
                         dbo.Employees ON dbo.AddToBox.EmployeeID = dbo.Employees.EmployeeID LEFT OUTER JOIN
                         dbo.Customers ON dbo.AddToBox.CustomerID = dbo.Customers.CustomerID LEFT OUTER JOIN
                         dbo.Customers AS Customers_1 ON Customers_1.CustomerID =
                             (SELECT        CustomerID
                               FROM            dbo.CustomersPayments AS CustomersPayments_1
                               WHERE        (CustomerPaymentID = dbo.AddToBox.CustomerPaymentID))

