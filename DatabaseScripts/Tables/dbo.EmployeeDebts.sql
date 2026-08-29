CREATE TABLE [dbo].[EmployeeDebts] (
    [EmployeeDebtsID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [EmployeeID] INT NULL,
    [AmountDebt] FLOAT NULL,
    [DateDebt] DATETIME NULL,
    [Purpose] NVARCHAR(MAX) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [AccountType] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.EmployeeDebts_dbo.Employees_EmployeeID] FOREIGN KEY ([EmployeeID]) REFERENCES [dbo].[Employees] ([EmployeeID]),
    CONSTRAINT [FK_dbo.EmployeeDebts_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
