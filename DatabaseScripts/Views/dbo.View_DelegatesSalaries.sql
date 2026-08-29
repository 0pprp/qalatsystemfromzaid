create   view [dbo].[View_DelegatesSalaries]
AS
WITH UserData AS (SELECT        UserID, UserName
                                           FROM            dbo.Users), DelegateData AS
    (SELECT        DelegateID, DelegateName
      FROM            dbo.Delegates)
    SELECT        dbo.DelegatesSalaries.DelegateSalaryID, dbo.DelegatesSalaries.DelegateID, dbo.DelegatesSalaries.UserID, dbo.DelegatesSalaries.SalaryAmount, dbo.DelegatesSalaries.SalaryDate, dbo.DelegatesSalaries.AsyncState, 
                              dbo.DelegatesSalaries.AsyncID, UserData_1.UserName, DelegateData_1.DelegateName, ISNULL(dbo.DelegatesSalaries.SalaryAmount * 1448, 0) AS SalaryAmountDenar
     FROM            dbo.DelegatesSalaries LEFT OUTER JOIN
                              UserData AS UserData_1 ON dbo.DelegatesSalaries.UserID = UserData_1.UserID LEFT OUTER JOIN
                              DelegateData AS DelegateData_1 ON dbo.DelegatesSalaries.DelegateID = DelegateData_1.DelegateID

