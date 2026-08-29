CREATE TABLE [dbo].[Permissions] (
    [PermissionID] INT IDENTITY(1,1) NOT NULL,
    [GroupID] INT NULL,
    [PermissionName] NVARCHAR(255) NULL,
    [PermissionNameState] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.Permissions_dbo.Groups_GroupID] FOREIGN KEY ([GroupID]) REFERENCES [dbo].[Groups] ([GroupID])
);
