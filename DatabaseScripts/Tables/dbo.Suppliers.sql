CREATE TABLE [dbo].[Suppliers] (
    [SupplierID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [CityID] INT NULL,
    [SupplierName] NVARCHAR(255) NULL,
    [Address] NVARCHAR(255) NULL,
    [Longitude] FLOAT NULL,
    [Latitude] FLOAT NULL,
    [PhoneNumber] NVARCHAR(255) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [SupplierImage] NVARCHAR(MAX) NULL,
    [SupplierState] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.Suppliers_dbo.Cities_CityID] FOREIGN KEY ([CityID]) REFERENCES [dbo].[Cities] ([CityID]),
    CONSTRAINT [FK_dbo.Suppliers_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
