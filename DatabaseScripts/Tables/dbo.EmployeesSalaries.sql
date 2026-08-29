CREATE TABLE [dbo].[EmployeesSalaries] (
    [EmployeeSalaryID] INT IDENTITY(1,1) NOT NULL,
    [EmployeeID] INT NULL,
    [UserID] INT NULL,
    [SalaryAmount] FLOAT NULL,
    [SalaryDate] DATETIME NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.EmployeesSalaries_dbo.Employees_EmployeeID] FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[Employees] ([EmployeeID]),
    CONSTRAINT [FK_dbo.EmployeesSalaries_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
