CREATE TABLE [dbo].[MerchantNotification] (
    [MerchantNotificationID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [MerchantID] INT NULL,
    [Title] NVARCHAR(255) NULL,
    [TypeNotification] NVARCHAR(255) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [IsRead] BIT NULL DEFAULT ('false'),
    [DateCreate] DATETIME NULL DEFAULT (getutcdate()),
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
