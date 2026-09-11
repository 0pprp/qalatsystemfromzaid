CREATE TABLE [dbo].[CustomerNotes] (
    [NoteID] INT IDENTITY(1,1) NOT NULL,
    [CustomerID] INT NOT NULL,
    [UserID] INT NULL,
    [NoteText] NVARCHAR(MAX) NOT NULL,
    [CreatedByName] NVARCHAR(150) NULL,
    [CreatedAtUtc] DATETIME2 NOT NULL CONSTRAINT [DF_CustomerNotes_CreatedAtUtc] DEFAULT (SYSUTCDATETIME()),
    [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_CustomerNotes] PRIMARY KEY CLUSTERED ([NoteID] ASC),
    CONSTRAINT [FK_CustomerNotes_Customers] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[Customers] ([CustomerID]),
    CONSTRAINT [FK_CustomerNotes_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
GO

CREATE NONCLUSTERED INDEX [IX_CustomerNotes_Customer_CreatedAt]
ON [dbo].[CustomerNotes] ([CustomerID] ASC, [CreatedAtUtc] DESC);
