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
GO

CREATE UNIQUE INDEX [UX_SalesEmployeeTrackPoints_ClientKey]
    ON [dbo].[SalesEmployeeTrackPoints] ([UserID], [ClientKey])
    WHERE [ClientKey] IS NOT NULL;
GO
