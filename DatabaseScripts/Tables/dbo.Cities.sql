CREATE TABLE [dbo].[Cities] (
    [CityID] INT IDENTITY(1,1) NOT NULL,
    [CityName] NVARCHAR(255) NULL,
    [UserID] INT NULL,
    [CityNameState] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.Cities_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
