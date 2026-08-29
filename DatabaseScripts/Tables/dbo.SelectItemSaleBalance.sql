CREATE TABLE [dbo].[SelectItemSaleBalance] (
    [SelectItemSaleBalanceID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [CustomerSaleBalanceID] INT NULL,
    [BalanceID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SelectItemSaleBalance_Balance] FOREIGN KEY ([BalanceID]) REFERENCES [dbo].[Balance] ([BalanceID]),
    CONSTRAINT [FK_SelectItemSaleBalance_CustomerSaleBalance] FOREIGN KEY ([CustomerSaleBalanceID]) REFERENCES [dbo].[CustomerSaleBalance] ([CustomerSaleBalanceID]),
    CONSTRAINT [FK_SelectItemSaleBalance_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
