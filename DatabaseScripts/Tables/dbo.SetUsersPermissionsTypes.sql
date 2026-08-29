CREATE TABLE [dbo].[SetUsersPermissionsTypes] (
    [SetUserPermissionTypeID] INT IDENTITY(1,1) NOT NULL,
    [PermissionTypeID] INT NULL,
    [UserID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SetUsersPermissionsTypes_dbo.PermissionsTypes_PermissionTypeID] FOREIGN KEY ([PermissionTypeID]) REFERENCES [dbo].[PermissionsTypes] ([PermissionTypeID]),
    CONSTRAINT [FK_dbo.SetUsersPermissionsTypes_dbo.Users_UserID] FOREIGN KEY ([UserID]) REFERENCES [dbo].[Users] ([UserID])
);
