CREATE TABLE [dbo].[PermissionsTypes] (
    [PermissionTypeID] INT IDENTITY(1,1) NOT NULL,
    [PermissionTypeName] NVARCHAR(255) NULL,
    [PermissionState] BIT NULL,
    [PermissionTypeNameState] BIT NULL,
    [PermissionTypeImage] NVARCHAR(MAX) NULL,
    [Notes] NVARCHAR(255) NULL,
    [State] BIT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL
);
