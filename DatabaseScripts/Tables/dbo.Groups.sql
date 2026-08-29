CREATE TABLE [dbo].[Groups] (
    [GroupID] INT IDENTITY(1,1) NOT NULL,
    [GroupName] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
