create   view [dbo].[View_WithdrawalFromBox]
AS
SELECT        WFB.WithdrawalFromBoxID, WFB.BoxID, WFB.Amount, WFB.Purpose, WFB.Notes, WFB.UserID, WFB.DateCreate, WFB.DateModify, WFB.CustomerID, WFB.EmployeeID, WFB.DelegateID, WFB.DocumentID, 
                         WFB.ExchangeItemID, WFB.TransferBoxID, WFB.BuyID, WFB.SupplierID, WFB.DelegateSalaryID, WFB.EmployeeSalaryID, WFB.AsyncState, WFB.AsyncID, WFB.BuyBalanceID, B.BoxName, U.UserName, C.CustomerName, 
                         E.EmployeeName, D.DelegateName, EI.ExchangeItemName, S.SupplierName, ISNULL(WFB.Amount * 1448, 0) AS AmountDenar
FROM            dbo.WithdrawalFromBox AS WFB LEFT OUTER JOIN
                         dbo.Boxes AS B ON WFB.BoxID = B.BoxID LEFT OUTER JOIN
                         dbo.Users AS U ON WFB.UserID = U.UserID LEFT OUTER JOIN
                         dbo.Customers AS C ON WFB.CustomerID = C.CustomerID LEFT OUTER JOIN
                         dbo.Employees AS E ON WFB.EmployeeID = E.EmployeeID LEFT OUTER JOIN
                         dbo.Delegates AS D ON WFB.DelegateID = D.DelegateID LEFT OUTER JOIN
                         dbo.ExchangeItems AS EI ON WFB.ExchangeItemID = EI.ExchangeItemID LEFT OUTER JOIN
                         dbo.Suppliers AS S ON WFB.SupplierID = S.SupplierID

