CREATE TABLE [dbo].[SelectItemSalesTemporaryRequest] (
    [SelectItemSalesTemporaryRequestID] INT IDENTITY(1,1) NOT NULL,
    [ItemID] INT NULL,
    [DelegateID] INT NULL,
    [Quantity] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectItemSalesTemporaryRequest_dbo.Delegates_DelegateID] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID]),
    CONSTRAINT [FK_dbo.SelectItemSalesTemporaryRequest_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID])
);
