CREATE TABLE [dbo].[CustomerNotification] (
    [CustomerNotificationID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [CustomerID] INT NULL,
    [Title] NVARCHAR(255) NULL,
    [TypeNotification] NVARCHAR(255) NULL,
    [Description] NVARCHAR(MAX) NULL,
    [IsRead] BIT NULL DEFAULT ('false'),
    [AsyncID] NVARCHAR(255) NULL,
    [DateCreate] DATETIME NULL DEFAULT (getutcdate()),
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
