CREATE TABLE [dbo].[DelegatesDebts] (
    [DelegateDebtID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [DelegateID] INT NULL,
    [AmountDebt] FLOAT NULL,
    [DateDebt] DATETIME NULL,
    [Purpose] NVARCHAR(MAX) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [AccountType] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.DelegatesDebts_dbo.Delegates_DelegateID] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID]),
    CONSTRAINT [FK_dbo.DelegatesDebts_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
