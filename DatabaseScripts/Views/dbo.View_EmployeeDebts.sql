create   view [dbo].[View_EmployeeDebts]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), EmployeeData AS
    (SELECT        EmployeeID, EmployeeName, CityID
      FROM            dbo.Employees), CityData AS
    (SELECT        CityID, CityName
      FROM            dbo.Cities)
    SELECT        dbo.EmployeeDebts.EmployeeDebtsID, dbo.EmployeeDebts.UserID, dbo.EmployeeDebts.EmployeeID, dbo.EmployeeDebts.AmountDebt, dbo.EmployeeDebts.DateDebt, dbo.EmployeeDebts.Purpose, 
                              dbo.EmployeeDebts.Notes, dbo.EmployeeDebts.AccountType, dbo.EmployeeDebts.AsyncState, dbo.EmployeeDebts.AsyncID, UserData_1.UserName, EmployeeData_1.EmployeeName, EmployeeData_1.CityID, 
                              CityData_1.CityName, ISNULL(dbo.EmployeeDebts.AmountDebt * 1448, 0) AS AmountDebtDenar
     FROM            dbo.EmployeeDebts LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.EmployeeDebts.UserID = UserData_1.UserID LEFT OUTER JOIN
                              EmployeeData AS EmployeeData_1 ON dbo.EmployeeDebts.EmployeeID = EmployeeData_1.EmployeeID LEFT OUTER JOIN
                              CityData AS CityData_1 ON EmployeeData_1.CityID = CityData_1.CityID

