CREATE TABLE [dbo].[Category] (
    [CategoryID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [CategoryName] NVARCHAR(255) NULL,
    [CategoryState] BIT NULL DEFAULT ('true'),
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
