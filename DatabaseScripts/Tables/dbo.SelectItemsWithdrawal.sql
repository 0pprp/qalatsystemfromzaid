CREATE TABLE [dbo].[SelectItemsWithdrawal] (
    [SelectItemWithdrawalID] INT IDENTITY(1,1) NOT NULL,
    [WithdrawalStoresID] INT NULL,
    [UserID] INT NULL,
    [ItemID] INT NULL,
    [Quantity] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectItemsWithdrawal_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID]),
    CONSTRAINT [FK_dbo.SelectItemsWithdrawal_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID]),
    CONSTRAINT [FK_dbo.SelectItemsWithdrawal_dbo.WithdrawalStores_WithdrawalStoresID] FOREIGN KEY ([WithdrawalStoresID]) REFERENCES [dbo].[WithdrawalStores] ([WithdrawalStoresID])
);
