CREATE TABLE [dbo].[Users] (
    [UserID] INT IDENTITY(1,1) NOT NULL,
    [UserName] NVARCHAR(255) NULL,
    [Email] NVARCHAR(255) NULL,
    [Password] NVARCHAR(255) NULL,
    [PhoneNumber] NVARCHAR(255) NULL,
    [Address] NVARCHAR(255) NULL,
    [UserState] BIT NULL,
    [UserImage] NVARCHAR(MAX) NULL,
    [BoxID] INT NULL,
    [AsyncState] BIT NULL,
    [AsyncID] NVARCHAR(255) NULL,
    [UserType] NVARCHAR(100) NULL,
    [CreatedDate] DATETIME NOT NULL DEFAULT (getdate()),
    [UpdatedDate] DATETIME NULL,
    CONSTRAINT [FK_dbo.Users_dbo.Boxes_BoxID] FOREIGN KEY ([BoxID]) REFERENCES [dbo].[Boxes] ([BoxID])
);
