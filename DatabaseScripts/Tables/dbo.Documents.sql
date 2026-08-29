CREATE TABLE [dbo].[Documents] (
    [DocumentID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [FromAccount] NVARCHAR(255) NULL,
    [ToAccount] NVARCHAR(255) NULL,
    [DocumentDateCreate] DATETIME NULL,
    [DocumentDateModify] DATETIME NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [DocumentType] NVARCHAR(255) NULL,
    [Amount] FLOAT NULL,
    [DocumentState] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.Documents_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
