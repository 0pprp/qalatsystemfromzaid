CREATE TABLE [dbo].[Boxes] (
    [BoxID] INT IDENTITY(1,1) NOT NULL,
    [BoxName] NVARCHAR(255) NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [BoxState] BIT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
