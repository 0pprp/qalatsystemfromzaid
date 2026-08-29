CREATE TABLE [dbo].[SelectPaymentCustomerTemporary] (
    [SelectPaymentCustomerTemporaryID] INT IDENTITY(1,1) NOT NULL,
    [DelegateID] INT NULL,
    [CustomerID] INT NULL,
    [Amount] FLOAT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [Location] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SelectPaymentCustomerTemporary_Customers] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[Customers] ([CustomerID]),
    CONSTRAINT [FK_SelectPaymentCustomerTemporary_Delegates] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID])
);
