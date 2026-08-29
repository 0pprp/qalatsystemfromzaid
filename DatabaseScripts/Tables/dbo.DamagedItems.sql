CREATE TABLE [dbo].[DamagedItems] (
    [DamagedItemID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [Reason] NVARCHAR(255) NULL,
    [DamagedItemDate] DATETIME NULL,
    [State] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.DamagedItems_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
