CREATE TABLE [dbo].[WithdrawalStores] (
    [WithdrawalStoresID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [State] BIT NULL,
    [WithdrawalStoresDate] DATETIME NULL,
    [StoreID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.WithdrawalStores_dbo.Stores_StoreID] FOREIGN KEY ([StoreID]) REFERENCES [dbo].[Stores] ([StoreID]),
    CONSTRAINT [FK_dbo.WithdrawalStores_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
