CREATE TABLE [dbo].[HostHistory] (
    [HostHistoryID] INT IDENTITY(1,1) NOT NULL,
    [DateCreate] DATETIME NULL DEFAULT (getutcdate()),
    [AsyncID] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL DEFAULT ('false'),
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
