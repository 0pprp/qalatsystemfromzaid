CREATE TABLE [dbo].[SelectItemsAddToStores] (
    [SelectItemAddToStoreID] INT IDENTITY(1,1) NOT NULL,
    [AddToStoreID] INT NULL,
    [UserID] INT NULL,
    [ItemID] INT NULL,
    [Quantity] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectItemsAddToStores_dbo.AddToStores_AddToStoreID] FOREIGN KEY ([AddToStoreID]) REFERENCES [dbo].[AddToStores] ([AddToStoreID]),
    CONSTRAINT [FK_dbo.SelectItemsAddToStores_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID]),
    CONSTRAINT [FK_dbo.SelectItemsAddToStores_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
