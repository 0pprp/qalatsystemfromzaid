CREATE TABLE [dbo].[SelectItemsSalesRequestOld] (
    [SelectItemsSalesRequestOldID] INT IDENTITY(1,1) NOT NULL,
    [CustomersSalesRequestOldID] INT NULL,
    [ItemID] INT NULL,
    [Quantity] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SelectItemsSalesRequestOld_CustomersSalesRequestOld] FOREIGN KEY ([CustomersSalesRequestOldID]) REFERENCES [dbo].[CustomersSalesRequestOld] ([CustomersSalesRequestOldID]),
    CONSTRAINT [FK_SelectItemsSalesRequestOld_Items] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID])
);
