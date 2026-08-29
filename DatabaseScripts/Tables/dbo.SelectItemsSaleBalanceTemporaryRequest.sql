CREATE TABLE [dbo].[SelectItemsSaleBalanceTemporaryRequest] (
    [SelectItemsSaleBalanceTemporaryRequestID] INT IDENTITY(1,1) NOT NULL,
    [DelegateID] INT NULL,
    [BalanceID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SelectItemsSaleBalanceTemporaryRequest_Balance] FOREIGN KEY ([BalanceID]) REFERENCES [dbo].[Balance] ([BalanceID]),
    CONSTRAINT [FK_SelectItemsSaleBalanceTemporaryRequest_Delegates] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID])
);
