CREATE TABLE [dbo].[BalanceType] (
    [BalanceTypeID] INT IDENTITY(1,1) NOT NULL,
    [BalanceTypeName] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [UserID] INT NULL,
    [State] BIT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_BalanceType_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
