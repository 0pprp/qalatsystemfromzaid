CREATE TABLE [dbo].[CustomerAddRequest] (
    [CustomerAddRequestID] INT IDENTITY(1,1) NOT NULL,
    [CityID] INT NULL,
    [CustomerName] NVARCHAR(255) NULL,
    [PhoneNumber] NVARCHAR(255) NULL,
    [Address] NVARCHAR(255) NULL,
    [ShopName] NVARCHAR(255) NULL,
    [NearestFunctionPoint] NVARCHAR(255) NULL,
    [Location] NVARCHAR(255) NULL,
    [DateCreate] DATETIME NULL DEFAULT (getutcdate()),
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
