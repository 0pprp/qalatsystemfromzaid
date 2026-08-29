CREATE TABLE [dbo].[Merchant] (
    [MerchantID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [CityID] INT NULL,
    [MerchantName] NVARCHAR(255) NULL,
    [ManagerName] NVARCHAR(255) NULL,
    [Password] NVARCHAR(255) NULL,
    [PhoneNumberMerchant] NVARCHAR(255) NULL,
    [PhoneNumberManager] NVARCHAR(255) NULL,
    [Address] NVARCHAR(255) NULL,
    [ImageManger] NVARCHAR(MAX) NULL,
    [ImageMerchant] NVARCHAR(MAX) NULL,
    [MerchantState] BIT NULL DEFAULT ('true'),
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
