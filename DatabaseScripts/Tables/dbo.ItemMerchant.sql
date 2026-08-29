CREATE TABLE [dbo].[ItemMerchant] (
    [ItemMerchantID] INT IDENTITY(1,1) NOT NULL,
    [MerchantID] INT NULL,
    [UserID] INT NULL,
    [CategoryID] INT NULL,
    [ItemMerchantName] NVARCHAR(255) NULL,
    [Price] FLOAT NULL,
    [Specifications] NVARCHAR(MAX) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [ItemMerchantState] BIT NULL DEFAULT ('true'),
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
