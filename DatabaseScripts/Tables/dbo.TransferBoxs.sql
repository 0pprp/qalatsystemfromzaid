CREATE TABLE [dbo].[TransferBoxs] (
    [TransferBoxID] INT IDENTITY(1,1) NOT NULL,
    [FromBoxID] INT NULL,
    [ToBoxID] INT NULL,
    [UserID] INT NULL,
    [Amount] FLOAT NULL,
    [Notes] NVARCHAR(MAX) NULL,
    [DateModify] DATETIME NULL,
    [DateCreate] DATETIME NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.TransferBoxs_dbo.Boxes_FromBoxID] FOREIGN KEY ([FromBoxID]) REFERENCES [dbo].[Boxes] ([BoxID]),
    CONSTRAINT [FK_dbo.TransferBoxs_dbo.Boxes_ToBoxID] FOREIGN KEY ([ToBoxID]) REFERENCES [dbo].[Boxes] ([BoxID]),
    CONSTRAINT [FK_dbo.TransferBoxs_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
