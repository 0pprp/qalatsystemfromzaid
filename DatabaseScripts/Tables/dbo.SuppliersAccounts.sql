CREATE TABLE [dbo].[SuppliersAccounts] (
    [SupplierAccountID] INT IDENTITY(1,1) NOT NULL,
    [SupplierID] INT NULL,
    [UserID] INT NULL,
    [BuyID] INT NULL,
    [Amount] FLOAT NULL,
    [AccountType] NVARCHAR(255) NULL,
    [AccountsDate] DATETIME NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [BuyBalanceID] INT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SuppliersAccounts_BuyBalance] FOREIGN KEY ([BuyBalanceID]) REFERENCES [dbo].[BuyBalance] ([BuyBalanceID]),
    CONSTRAINT [FK_dbo.SuppliersAccounts_dbo.Buys_BuyID] FOREIGN KEY ([BuyID]) REFERENCES [dbo].[Buys] ([BuyID]),
    CONSTRAINT [FK_dbo.SuppliersAccounts_dbo.Suppliers_SupplierID] FOREIGN KEY ([SupplierID]) REFERENCES [dbo].[Suppliers] ([SupplierID]),
    CONSTRAINT [FK_dbo.SuppliersAccounts_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
