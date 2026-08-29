CREATE TABLE [dbo].[ExchangeItemsDebts] (
    [ExchangeItemDebtID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [ExchangeItemID] INT NULL,
    [AmountDebt] FLOAT NULL,
    [DateDebt] DATETIME NULL,
    [Purpose] NVARCHAR(MAX) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [AccountType] NVARCHAR(MAX) NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.ExchangeItemsDebts_dbo.ExchangeItems_ExchangeItemID] FOREIGN KEY ([ExchangeItemID]) REFERENCES [dbo].[ExchangeItems] ([ExchangeItemID]),
    CONSTRAINT [FK_dbo.ExchangeItemsDebts_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
