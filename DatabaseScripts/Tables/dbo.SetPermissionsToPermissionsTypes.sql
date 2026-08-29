CREATE TABLE [dbo].[SetPermissionsToPermissionsTypes] (
    [SetPermissionToPermissionTypeID] INT IDENTITY(1,1) NOT NULL,
    [PermissionTypeID] INT NULL,
    [PermissionID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.SetPermissionsToPermissionsTypes_dbo.Permissions_PermissionID] FOREIGN KEY ([PermissionID]) REFERENCES [dbo].[Permissions] ([PermissionID]),
    CONSTRAINT [FK_dbo.SetPermissionsToPermissionsTypes_dbo.PermissionsTypes_PermissionTypeID] FOREIGN KEY ([PermissionTypeID]) REFERENCES [dbo].[PermissionsTypes] ([PermissionTypeID])
);
