CREATE TABLE [dbo].[ItemMerchantImageTemp] (
    [ItemMerchantImageTempID] INT IDENTITY(1,1) NOT NULL,
    [MerchantID] INT NULL,
    [Image] NVARCHAR(MAX) NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
