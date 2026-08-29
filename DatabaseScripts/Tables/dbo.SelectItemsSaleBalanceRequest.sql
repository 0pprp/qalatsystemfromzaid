CREATE TABLE [dbo].[SelectItemsSaleBalanceRequest] (
    [SelectItemsSaleBalanceRequestID] INT IDENTITY(1,1) NOT NULL,
    [CustomerSaleBalanceRequestID] INT NULL,
    [BalanceID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SelectItemsSaleBalanceRequest_Balance] FOREIGN KEY ([BalanceID]) REFERENCES [dbo].[Balance] ([BalanceID]),
    CONSTRAINT [FK_SelectItemsSaleBalanceRequest_CustomerSaleBalanceRequest] FOREIGN KEY ([CustomerSaleBalanceRequestID]) REFERENCES [dbo].[CustomerSaleBalanceRequest] ([CustomerSaleBalanceRequestID])
);
