CREATE TABLE [dbo].[SupplierItemRequest] (
    [SupplierItemRequestID] INT IDENTITY(1,1) NOT NULL,
    [SupplierRequestID] INT NULL,
    [ItemName] NVARCHAR(255) NULL,
    [ItemPrice] FLOAT NULL,
    [Quantity] INT NULL,
    [ItemImage] NVARCHAR(MAX) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [ItemImageByte] IMAGE NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SupplierItemRequest_SupplierRequest] FOREIGN KEY ([SupplierRequestID]) REFERENCES [dbo].[SupplierRequest] ([SupplierRequestID])
);
