CREATE TABLE [dbo].[SelectedBoxes] (
    [SelectBoxID] INT IDENTITY(1,1) NOT NULL,
    [BoxID] INT NULL,
    [UserID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectedBoxes_dbo.Boxes_BoxID] FOREIGN KEY ([BoxID]) REFERENCES [dbo].[Boxes] ([BoxID]),
    CONSTRAINT [FK_dbo.SelectedBoxes_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
