CREATE TABLE [dbo].[Stores] (
    [StoreID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [StoreName] NVARCHAR(255) NULL,
    [StorePlace] NVARCHAR(255) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [CityID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [State] BIT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.Stores_dbo.Cities_CityID] FOREIGN KEY ([CityID]) REFERENCES [dbo].[Cities] ([CityID]),
    CONSTRAINT [FK_dbo.Stores_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
