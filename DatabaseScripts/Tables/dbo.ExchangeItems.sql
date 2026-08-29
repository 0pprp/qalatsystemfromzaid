CREATE TABLE [dbo].[ExchangeItems] (
    [ExchangeItemID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [CityID] INT NULL,
    [ExchangeItemName] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [LimitAmount] FLOAT NULL,
    [ExchangeItemsState] BIT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.ExchangeItems_dbo.Cities_CityID] FOREIGN KEY ([CityID]) REFERENCES [dbo].[Cities] ([CityID]),
    CONSTRAINT [FK_dbo.ExchangeItems_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
