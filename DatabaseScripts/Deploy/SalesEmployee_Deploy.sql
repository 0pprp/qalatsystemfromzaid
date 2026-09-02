/*
  SalesEmployee_Deploy.sql
  Apply this script on EVERY province database used by GetAdmin
  before using the sales-employee Flutter app / BE_SalesEmployee gateway.

  Example (sqlcmd):
    sqlcmd -S YOUR_SQL_HOST -d DatabaseCompanyNajaf -E -I -i SalesEmployee_Deploy.sql

  After deploy:
    - Publish/restart BE_Company (adds موظف مبيعات login + shift/track/rating APIs)
    - Run BE_SalesEmployee
    - Create a user with UserType = N'موظف مبيعات' from المستخدمين
*/
SET ANSI_NULLS ON;
SET QUOTED_IDENTIFIER ON;
SET NOCOUNT ON;
GO

IF OBJECT_ID('dbo.SalesEmployeeShifts', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SalesEmployeeShifts] (
        [ShiftID] INT IDENTITY(1,1) NOT NULL,
        [UserID] INT NOT NULL,
        [ShiftDate] DATE NOT NULL,
        [StartedAt] DATETIME NOT NULL,
        [EndsAt] DATETIME NOT NULL,
        CONSTRAINT [PK_SalesEmployeeShifts] PRIMARY KEY CLUSTERED ([ShiftID] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_SalesEmployeeShifts_UserDate'
      AND object_id = OBJECT_ID(N'dbo.SalesEmployeeShifts')
)
BEGIN
    CREATE UNIQUE INDEX [UX_SalesEmployeeShifts_UserDate]
        ON [dbo].[SalesEmployeeShifts] ([UserID], [ShiftDate]);
END
GO

IF OBJECT_ID('dbo.SalesEmployeeTrackPoints', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SalesEmployeeTrackPoints] (
        [PointID] INT IDENTITY(1,1) NOT NULL,
        [ShiftID] INT NOT NULL,
        [UserID] INT NOT NULL,
        [RecordedAt] DATETIME NOT NULL,
        [Latitude] FLOAT NOT NULL,
        [Longitude] FLOAT NOT NULL,
        [Accuracy] FLOAT NULL,
        [ClientKey] NVARCHAR(64) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_SalesEmployeeTrackPoints_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_SalesEmployeeTrackPoints] PRIMARY KEY CLUSTERED ([PointID] ASC)
    );
END
GO

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_SalesEmployeeTrackPoints_ClientKey'
      AND object_id = OBJECT_ID(N'dbo.SalesEmployeeTrackPoints')
)
BEGIN
    CREATE UNIQUE INDEX [UX_SalesEmployeeTrackPoints_ClientKey]
        ON [dbo].[SalesEmployeeTrackPoints] ([UserID], [ClientKey])
        WHERE [ClientKey] IS NOT NULL;
END
GO

IF OBJECT_ID('dbo.SalesCustomerRatings', 'U') IS NULL
BEGIN
    CREATE TABLE [dbo].[SalesCustomerRatings] (
        [RatingID] INT IDENTITY(1,1) NOT NULL,
        [UserID] INT NOT NULL,
        [CustomerID] INT NULL,
        [CustomerName] NVARCHAR(200) NULL,
        [PhoneNumber] NVARCHAR(50) NULL,
        [RatingLevel] INT NOT NULL,
        [Notes] NVARCHAR(MAX) NULL,
        [RejectionReason] NVARCHAR(MAX) NULL,
        [CreatedAt] DATETIME NOT NULL CONSTRAINT [DF_SalesCustomerRatings_CreatedAt] DEFAULT (GETDATE()),
        CONSTRAINT [PK_SalesCustomerRatings] PRIMARY KEY CLUSTERED ([RatingID] ASC)
    );
END
GO
