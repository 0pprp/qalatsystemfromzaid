CREATE TABLE [dbo].[DelegatesSalaries] (
    [DelegateSalaryID] INT IDENTITY(1,1) NOT NULL,
    [DelegateID] INT NULL,
    [UserID] INT NULL,
    [SalaryAmount] FLOAT NULL,
    [SalaryDate] DATETIME NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.DelegatesSalaries_dbo.Delegates_DelegateID] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID]),
    CONSTRAINT [FK_dbo.DelegatesSalaries_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
