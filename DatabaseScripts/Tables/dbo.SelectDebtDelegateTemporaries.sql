CREATE TABLE [dbo].[SelectDebtDelegateTemporaries] (
    [SelectDebtDelegateTemporaryID] INT IDENTITY(1,1) NOT NULL,
    [CityID] INT NULL,
    [DelegateID] INT NULL,
    [BoxID] INT NULL,
    [UserID] INT NULL,
    [TypeDocument] NVARCHAR(255) NULL,
    [Amount] FLOAT NULL,
    [DateDocument] DATETIME NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SelectDebtDelegateTemporaries_dbo.Boxes_BoxID] FOREIGN KEY ([BoxID]) REFERENCES [dbo].[Boxes] ([BoxID]),
    CONSTRAINT [FK_dbo.SelectDebtDelegateTemporaries_dbo.Cities_CityID] FOREIGN KEY ([CityID]) REFERENCES [dbo].[Cities] ([CityID]),
    CONSTRAINT [FK_dbo.SelectDebtDelegateTemporaries_dbo.Delegates_DelegateID] FOREIGN KEY ([DelegateID]) REFERENCES [dbo].[Delegates] ([DelegateID]),
    CONSTRAINT [FK_dbo.SelectDebtDelegateTemporaries_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
