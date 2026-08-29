CREATE TABLE [dbo].[CompanyInformation] (
    [CompanyInformationID] INT IDENTITY(1,1) NOT NULL,
    [CompanyName] NVARCHAR(255) NULL,
    [ManagerName] NVARCHAR(255) NULL,
    [PhoneNumber] NVARCHAR(255) NULL,
    [Email] NVARCHAR(255) NULL,
    [Address] NVARCHAR(255) NULL,
    [DateCreate] DATETIME NULL,
    [Longitude] FLOAT NULL,
    [Latitude] FLOAT NULL,
    [Logo] NVARCHAR(MAX) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
