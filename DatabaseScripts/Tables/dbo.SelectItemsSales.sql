CREATE TABLE [dbo].[SelectItemsSales] (
    [SelectItemsSaleID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [CustomerSaleID] INT NULL,
    [ItemID] INT NULL,
    [Quantity] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectItemsSales_dbo.CustomersSales_CustomerSaleID] FOREIGN KEY ([CustomerSaleID]) REFERENCES [dbo].[CustomersSales] ([CustomerSaleID]),
    CONSTRAINT [FK_dbo.SelectItemsSales_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID]),
    CONSTRAINT [FK_dbo.SelectItemsSales_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
