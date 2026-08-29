CREATE TABLE [dbo].[SelectItemsTransferStores] (
    [SelectItemTransferStoreID] INT IDENTITY(1,1) NOT NULL,
    [TransferStoreID] INT NULL,
    [UserID] INT NULL,
    [ItemID] INT NULL,
    [Quantity] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectItemsTransferStores_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID]),
    CONSTRAINT [FK_dbo.SelectItemsTransferStores_dbo.TransferStores_TransferStoreID] FOREIGN KEY ([TransferStoreID]) REFERENCES [dbo].[TransferStores] ([TransferStoreID]),
    CONSTRAINT [FK_dbo.SelectItemsTransferStores_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
