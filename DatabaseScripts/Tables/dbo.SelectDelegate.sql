CREATE TABLE [dbo].[SelectDelegate] (
    [SelectDelegateID] INT IDENTITY(1,1) NOT NULL,
    [DelegateFatherID] INT NULL,
    [DelegateChildID] INT NULL,
    [UserID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectDelegate_dbo.Delegates_DelegateChildID] FOREIGN KEY ([DelegateChildID]) REFERENCES [dbo].[Delegates] ([DelegateID]),
    CONSTRAINT [FK_dbo.SelectDelegate_dbo.Delegates_DelegateFatherID] FOREIGN KEY ([DelegateFatherID]) REFERENCES [dbo].[Delegates] ([DelegateID]),
    CONSTRAINT [FK_dbo.SelectDelegate_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
