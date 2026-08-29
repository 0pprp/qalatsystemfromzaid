CREATE TABLE [dbo].[DollarAmount] (
    [DollarAmountID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [Amount] FLOAT NULL,
    [LastDateModify] DATETIME NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.DollarAmount_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
