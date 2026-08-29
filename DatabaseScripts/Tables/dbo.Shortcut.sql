CREATE TABLE [dbo].[Shortcut] (
    [ShortcutID] INT IDENTITY(1,1) NOT NULL,
    [ShortcutName] NVARCHAR(255) NULL,
    [ShortcutImage] NVARCHAR(MAX) NULL,
    [ShortcutState] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
