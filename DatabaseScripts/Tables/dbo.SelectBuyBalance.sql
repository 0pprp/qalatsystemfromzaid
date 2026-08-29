CREATE TABLE [dbo].[SelectBuyBalance] (
    [SelectBuyBalanceID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [BalanceID] INT NULL,
    [BuyBalanceID] INT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SelectBuyBalance_Balance] FOREIGN KEY ([BalanceID]) REFERENCES [dbo].[Balance] ([BalanceID]),
    CONSTRAINT [FK_SelectBuyBalance_BuyBalance] FOREIGN KEY ([BuyBalanceID]) REFERENCES [dbo].[BuyBalance] ([BuyBalanceID]),
    CONSTRAINT [FK_SelectBuyBalance_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
