CREATE TABLE [dbo].[BuyBalance] (
    [BuyBalanceID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [SupplierID] INT NULL,
    [StoreBalanceID] INT NULL,
    [BoxID] INT NULL,
    [BoundNumber] INT NULL,
    [DateCreate] DATETIME NULL,
    [DateModify] DATETIME NULL,
    [BuyBalanceState] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_BuyBalance_StoreBalance] FOREIGN KEY ([StoreBalanceID]) REFERENCES [dbo].[StoreBalance] ([StoreBalanceID]),
    CONSTRAINT [FK_BuyBalance_Boxes] FOREIGN KEY ([BoxID]) REFERENCES [dbo].[Boxes] ([BoxID]),
    CONSTRAINT [FK_BuyBalance_Suppliers] FOREIGN KEY ([SupplierID]) REFERENCES [dbo].[Suppliers] ([SupplierID]),
    CONSTRAINT [FK_BuyBalance_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
