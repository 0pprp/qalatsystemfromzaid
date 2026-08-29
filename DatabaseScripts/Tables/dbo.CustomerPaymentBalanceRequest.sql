CREATE TABLE [dbo].[CustomerPaymentBalanceRequest] (
    [CustomerPaymentBalanceRequestID] INT IDENTITY(1,1) NOT NULL,
    [CustomerID] INT NULL,
    [DateCreate] DATETIME NULL,
    [BoundNumber] INT NULL,
    [DelegateID] INT NULL,
    [AccountZero] BIT NULL,
    [DelegateState] BIT NULL,
    [Amount] FLOAT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [SelectState] BIT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_CustomerPaymentBalanceRequest_Customers] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[Customers] ([CustomerID]),
    CONSTRAINT [FK_CustomerPaymentBalanceRequest_Delegates] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID])
);
