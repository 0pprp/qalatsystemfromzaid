CREATE TABLE [dbo].[SaleBalanceLimit] (
    [SaleBalanceLimitID] INT IDENTITY(1,1) NOT NULL,
    [DelegateID] INT NULL,
    [DateSale] DATETIME NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SaleBalanceLimit_Delegates] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID])
);
