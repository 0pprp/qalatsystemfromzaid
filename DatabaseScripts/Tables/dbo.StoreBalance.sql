CREATE TABLE [dbo].[StoreBalance] (
    [StoreBalanceID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [CityID] INT NULL,
    [StoreBalanceName] NVARCHAR(255) NULL,
    [StoreBalancePlace] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [Notes] NVARCHAR(255) NULL,
    [State] BIT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_StoreBalance_Cities] FOREIGN KEY ([CityID]) REFERENCES [dbo].[Cities] ([CityID]),
    CONSTRAINT [FK_StoreBalance_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
