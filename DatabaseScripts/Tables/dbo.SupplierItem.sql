CREATE TABLE [dbo].[SupplierItem] (
    [SupplierItemID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [SupplierID] INT NULL,
    [ItemName] NVARCHAR(255) NULL,
    [ItemPrice] FLOAT NULL,
    [Quantity] INT NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [ItemImage] NVARCHAR(MAX) NULL,
    [ItemImageByte] IMAGE NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SupplierItem_Suppliers] FOREIGN KEY ([SupplierID]) REFERENCES [dbo].[Suppliers] ([SupplierID]),
    CONSTRAINT [FK_SupplierItem_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
