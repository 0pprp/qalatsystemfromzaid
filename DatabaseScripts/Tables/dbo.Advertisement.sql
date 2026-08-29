CREATE TABLE [dbo].[Advertisement] (
    [AdvertisementID] INT IDENTITY(1,1) NOT NULL,
    [Description] NVARCHAR(MAX) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
