CREATE TABLE [dbo].[SelectItemsSaleBalanceOldRequest] (
    [SelectItemsSaleBalanceOldRequestID] INT IDENTITY(1,1) NOT NULL,
    [CustomerSaleBalanceRequestOldID] INT NULL,
    [BalanceID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SelectItemsSaleBalanceOldRequest_Balance] FOREIGN KEY ([BalanceID]) REFERENCES [dbo].[Balance] ([BalanceID]),
    CONSTRAINT [FK_SelectItemsSaleBalanceOldRequest_CustomerSaleBalanceRequestOld] FOREIGN KEY ([CustomerSaleBalanceRequestOldID]) REFERENCES [dbo].[CustomerSaleBalanceRequestOld] ([CustomerSaleBalanceRequestOldID])
);
