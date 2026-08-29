CREATE TABLE [dbo].[BuysItems] (
    [BuyItemID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [BuyID] INT NULL,
    [ItemID] INT NULL,
    [Quantity] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.BuysItems_dbo.Buys_BuyID] FOREIGN KEY ([BuyID]) REFERENCES [dbo].[Buys] ([BuyID]),
    CONSTRAINT [FK_dbo.BuysItems_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID]),
    CONSTRAINT [FK_dbo.BuysItems_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
