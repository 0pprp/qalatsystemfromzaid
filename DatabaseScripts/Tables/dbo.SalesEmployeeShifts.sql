CREATE TABLE [dbo].[SalesEmployeeShifts] (
    [ShiftID] INT IDENTITY(1,1) NOT NULL,
    [UserID] INT NOT NULL,
    [ShiftDate] DATE NOT NULL,
    [StartedAt] DATETIME NOT NULL,
    [EndsAt] DATETIME NOT NULL,
    CONSTRAINT [PK_SalesEmployeeShifts] PRIMARY KEY CLUSTERED ([ShiftID] ASC)
);
GO

CREATE UNIQUE INDEX [UX_SalesEmployeeShifts_UserDate]
    ON [dbo].[SalesEmployeeShifts] ([UserID], [ShiftDate]);
GO
