CREATE TABLE [dbo].[Activities] (
    [ActivityID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NULL,
    [ActivityDescription] NVARCHAR(MAX) NULL,
    [ActivityDate] DATETIME NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.Activities_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
