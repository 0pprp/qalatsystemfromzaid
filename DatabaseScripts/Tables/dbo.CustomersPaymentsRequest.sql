CREATE TABLE [dbo].[CustomersPaymentsRequest] (
    [CustomersPaymentsRequestID] INT IDENTITY(1,1) NOT NULL,
    [CustomerID] INT NULL,
    [PaymentDate] DATETIME NULL,
    [BoundNumber] INT NULL,
    [DelegateID] INT NULL,
    [AccountZero] BIT NULL,
    [DelegateState] BIT NULL,
    [Amount] FLOAT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [SelectState] BIT NULL,
    [Location] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.CustomersPaymentsRequest_dbo.Customers_CustomerID] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[Customers] ([CustomerID]),
    CONSTRAINT [FK_dbo.CustomersPaymentsRequest_dbo.Delegates_DelegateID] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID])
);
