CREATE TABLE [dbo].[BarcodeItems] (
    [BarcodeItemID] INT IDENTITY(1,1) NOT NULL,
    [ItemID] INT NULL,
    [UserID] INT NULL,
    [Barcode] NVARCHAR(MAX) NULL,
    [StoreID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.BarcodeItems_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID]),
    CONSTRAINT [FK_dbo.BarcodeItems_dbo.Stores_StoreID] FOREIGN KEY ([StoreID]) REFERENCES [dbo].[Stores] ([StoreID]),
    CONSTRAINT [FK_dbo.BarcodeItems_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
