CREATE TABLE [dbo].[SupplierRequest] (
    [SupplierRequestID] INT IDENTITY(1,1) NOT NULL,
    [CityID] INT NULL,
    [CompanyName] NVARCHAR(255) NULL,
    [Manager] NVARCHAR(255) NULL,
    [PhoneNumber] NVARCHAR(255) NULL,
    [CompanyImage] NVARCHAR(MAX) NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_SupplierRequest_Cities] FOREIGN KEY ([CityID]) REFERENCES [dbo].[Cities] ([CityID])
);
