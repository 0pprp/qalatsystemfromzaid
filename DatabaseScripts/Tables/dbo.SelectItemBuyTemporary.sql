CREATE TABLE [dbo].[SelectItemBuyTemporary] (
    [SelectItemBuyTemporaryID] INT IDENTITY(1,1) NOT NULL,
    [ItemID] INT NULL,
    [Quantity] INT NULL,
    [UserID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectItemBuyTemporary_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID]),
    CONSTRAINT [FK_dbo.SelectItemBuyTemporary_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
