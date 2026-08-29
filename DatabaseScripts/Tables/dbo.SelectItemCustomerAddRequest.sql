CREATE TABLE [dbo].[SelectItemCustomerAddRequest] (
    [SelectItemCustomerAddRequestID] INT IDENTITY(1,1) NOT NULL,
    [CustomerAddRequestID] INT NULL,
    [ItemMerchantID] INT NULL,
    [Quantity] INT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
