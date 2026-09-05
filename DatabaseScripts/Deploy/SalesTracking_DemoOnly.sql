-- RUN ONLY ON DatabaseCompanyNajaf_DEMO.
-- Do not execute against DatabaseCompany or DatabaseCompanyNajaf.
IF DB_NAME() <> N'DatabaseCompanyNajaf_DEMO'
BEGIN
    RAISERROR(N'SalesTracking script is allowed only on DatabaseCompanyNajaf_DEMO.', 16, 1);
    RETURN;
END;

IF OBJECT_ID(N'dbo.SalesWorkShifts', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesWorkShifts (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        EmployeeName NVARCHAR(200) NULL,
        CityValue NVARCHAR(100) NULL,
        CityName NVARCHAR(200) NULL,
        StartedAtUtc DATETIME NOT NULL,
        StartedAtIraq DATETIME NOT NULL,
        CutoffAtUtc DATETIME NOT NULL,
        Status NVARCHAR(20) NOT NULL,
        ClosedAtUtc DATETIME NULL,
        CloseReason NVARCHAR(50) NULL,
        CreatedAt DATETIME NOT NULL CONSTRAINT DF_SalesWorkShifts_CreatedAt DEFAULT (GETUTCDATE())
    );
    CREATE INDEX IX_SalesWorkShifts_EmployeeStatus ON dbo.SalesWorkShifts (EmployeeId, Status, CutoffAtUtc);
END;

IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_SalesWorkShifts_OneActive' AND object_id = OBJECT_ID(N'dbo.SalesWorkShifts')
)
BEGIN
    CREATE UNIQUE INDEX UX_SalesWorkShifts_OneActive
        ON dbo.SalesWorkShifts (EmployeeId)
        WHERE Status = N'Active';
END;

IF OBJECT_ID(N'dbo.SalesLocationPoints', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesLocationPoints (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        ShiftId INT NOT NULL,
        Latitude DECIMAL(9,6) NOT NULL,
        Longitude DECIMAL(9,6) NOT NULL,
        Accuracy FLOAT NULL,
        Speed FLOAT NULL,
        Heading FLOAT NULL,
        Altitude FLOAT NULL,
        CapturedAtUtc DATETIME NOT NULL,
        ReceivedAtUtc DATETIME NOT NULL,
        DeviceSequence BIGINT NOT NULL,
        DeviceSessionId NVARCHAR(100) NULL,
        CONSTRAINT FK_SalesLocationPoints_Shifts FOREIGN KEY (ShiftId) REFERENCES dbo.SalesWorkShifts (Id)
    );
    CREATE UNIQUE INDEX UX_SalesLocationPoints_ShiftSequence
        ON dbo.SalesLocationPoints (ShiftId, DeviceSequence);
END;

IF COL_LENGTH(N'dbo.SalesLocationPoints', N'IsOfficial') IS NULL
    ALTER TABLE dbo.SalesLocationPoints ADD IsOfficial BIT NOT NULL CONSTRAINT DF_SalesLocationPoints_IsOfficial DEFAULT (0);
IF COL_LENGTH(N'dbo.SalesLocationPoints', N'OfficialSlotUtc') IS NULL
    ALTER TABLE dbo.SalesLocationPoints ADD OfficialSlotUtc DATETIME NULL;
IF COL_LENGTH(N'dbo.SalesLocationPoints', N'ActualCapturedAtUtc') IS NULL
    ALTER TABLE dbo.SalesLocationPoints ADD ActualCapturedAtUtc DATETIME NULL;
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'UX_SalesLocationPoints_ShiftOfficialSlot' AND object_id = OBJECT_ID(N'dbo.SalesLocationPoints')
)
    CREATE UNIQUE INDEX UX_SalesLocationPoints_ShiftOfficialSlot
        ON dbo.SalesLocationPoints (ShiftId, OfficialSlotUtc)
        WHERE OfficialSlotUtc IS NOT NULL;

IF OBJECT_ID(N'dbo.SalesTrackingEvents', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.SalesTrackingEvents (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        EmployeeId INT NOT NULL,
        ShiftId INT NULL,
        EventType NVARCHAR(80) NOT NULL,
        OccurredAtUtc DATETIME NOT NULL,
        Metadata NVARCHAR(400) NULL
    );
    CREATE INDEX IX_SalesTrackingEvents_Employee ON dbo.SalesTrackingEvents (EmployeeId, OccurredAtUtc);
END;
