CREATE TABLE [dbo].[LinkCustomer] (
    [LinkCustomerID] INT IDENTITY(1,1) NOT NULL,
    [CityName] NVARCHAR(255) NULL,
    [Link] NVARCHAR(255) NULL,
    [DatabaseLink] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
