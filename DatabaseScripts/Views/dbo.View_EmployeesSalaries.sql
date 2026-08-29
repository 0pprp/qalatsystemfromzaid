create   view [dbo].[View_EmployeesSalaries]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), EmployeeData AS
    (SELECT        EmployeeID, EmployeeName
      FROM            dbo.Employees)
    SELECT        dbo.EmployeesSalaries.EmployeeSalaryID, dbo.EmployeesSalaries.EmployeeID, dbo.EmployeesSalaries.UserID, dbo.EmployeesSalaries.SalaryAmount, dbo.EmployeesSalaries.SalaryDate, 
                              dbo.EmployeesSalaries.AsyncState, dbo.EmployeesSalaries.AsyncID, ISNULL(dbo.EmployeesSalaries.SalaryAmount * 1448, 0) AS SalaryAmountDenar, UserData_1.UserName, EmployeeData_1.EmployeeName
     FROM            dbo.EmployeesSalaries LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.EmployeesSalaries.UserID = UserData_1.UserID LEFT OUTER JOIN
                              EmployeeData AS EmployeeData_1 ON dbo.EmployeesSalaries.EmployeeID = EmployeeData_1.EmployeeID

