CREATE TABLE [dbo].[TransferStores] (
    [TransferStoreID] INT IDENTITY(1,1) NOT NULL,
    [FromStoreID] INT NULL,
    [ToStoreID] INT NULL,
    [UserID] INT NULL,
    [TransferStoreDate] DATETIME NULL,
    [State] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.TransferStores_dbo.Stores_FromStoreID] FOREIGN KEY ([FromStoreID]) REFERENCES [dbo].[Stores] ([StoreID]),
    CONSTRAINT [FK_dbo.TransferStores_dbo.Stores_ToStoreID] FOREIGN KEY ([ToStoreID]) REFERENCES [dbo].[Stores] ([StoreID]),
    CONSTRAINT [FK_dbo.TransferStores_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
