CREATE TABLE [dbo].[CustomerNotes] (
    [NoteID] INT IDENTITY(1,1) NOT NULL,
    [CustomerID] INT NOT NULL,
    [UserID] INT NOT NULL,
    [NoteText] NVARCHAR(MAX) NOT NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (GETDATE()),
    CONSTRAINT [PK_CustomerNotes] PRIMARY KEY CLUSTERED ([NoteID] ASC),
    CONSTRAINT [FK_CustomerNotes_Customers] FOREIGN KEY ([CustomerID]) REFERENCES [dbo].[Customers] ([CustomerID]),
    CONSTRAINT [FK_CustomerNotes_Users] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
