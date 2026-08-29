CREATE TABLE [dbo].[SelectItemsSalesRequest] (
    [SelectItemsSalesRequestID] INT IDENTITY(1,1) NOT NULL,
    [CustomerSaleRequestID] INT NULL,
    [ItemID] INT NULL,
    [Quantity] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectItemsSalesRequest_dbo.CustomersSalesRequest_CustomerSaleRequestID] FOREIGN KEY ([CustomerSaleRequestID]) REFERENCES [dbo].[CustomersSalesRequest] ([CustomerSaleRequestID]),
    CONSTRAINT [FK_dbo.SelectItemsSalesRequest_dbo.Items_ItemID] FOREIGN KEY ([ItemID]) REFERENCES [dbo].[Items] ([ItemID])
);
