CREATE TABLE [dbo].[SelectItemCustomerTemp] (
    [SelectItemCustomerTempID] INT IDENTITY(1,1) NOT NULL,
    [CustomerID] INT NULL,
    [ItemMerchantID] INT NULL,
    [Quantity] INT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
