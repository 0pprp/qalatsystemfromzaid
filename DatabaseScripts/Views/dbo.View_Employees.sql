create   view [dbo].[View_Employees]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), CityData AS
    (SELECT        CityID, CityName
      FROM            dbo.Cities), EmployeeDebtsSummary AS
    (SELECT        EmployeeID, SUM(CASE WHEN AccountType = N'علينا' THEN AmountDebt * 1448 ELSE 0 END) AS TotalDebtsOnUs, SUM(CASE WHEN AccountType = N'لنا' THEN AmountDebt * 1448 ELSE 0 END) AS TotalDebtsToUs
      FROM            dbo.EmployeeDebts
      GROUP BY EmployeeID)
    SELECT        dbo.Employees.EmployeeID, dbo.Employees.UserID, dbo.Employees.CityID, dbo.Employees.EmployeeName, dbo.Employees.DateOfBirth, dbo.Employees.Address, dbo.Employees.PhoneNumber, 
                              dbo.Employees.AcademicAchievement, dbo.Employees.CV, dbo.Employees.Attachments, dbo.Employees.DateOfJoin, dbo.Employees.EmployeeImage, dbo.Employees.Notes, dbo.Employees.EmployeeState, 
                              dbo.Employees.Salary, dbo.Employees.AsyncState, dbo.Employees.AsyncID, UserData_1.UserName, CityData_1.CityName, dbo.Employees.Salary * 1448 AS SalaryDenar, 
                              ISNULL(EmployeeDebtsSummary_1.TotalDebtsOnUs, 0) - ISNULL(EmployeeDebtsSummary_1.TotalDebtsToUs, 0) AS AmountAccount, dbo.Employees.Salary * 1448 - ISNULL(EmployeeDebtsSummary_1.TotalDebtsOnUs, 0) 
                              - ISNULL(EmployeeDebtsSummary_1.TotalDebtsToUs, 0) AS FinalSalaryDenar
     FROM            dbo.Employees LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.Employees.UserID = UserData_1.UserID LEFT OUTER JOIN
                              CityData AS CityData_1 ON dbo.Employees.CityID = CityData_1.CityID LEFT OUTER JOIN
                              EmployeeDebtsSummary AS EmployeeDebtsSummary_1 ON dbo.Employees.EmployeeID = EmployeeDebtsSummary_1.EmployeeID

