CREATE TABLE [dbo].[ItemMerchantImage] (
    [ItemMerchantImageID] INT IDENTITY(1,1) NOT NULL,
    [ItemMerchantID] INT NULL,
    [Image] NVARCHAR(MAX) NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
