-- Daily work-time target: what the agent is told, and the dashboard alert for a day closed before the target.

-- One row per thing the agent was told about a day:
--   Reached: work time reached the target (one per day; RefAt = start of the Iraq day). The app shows a day summary
--            once, SeenAt is set when the agent closes it.
--   Idle:    two hours without a pin while the target is not reached (one per last pin; RefAt = that pin's time).
CREATE TABLE agents.WorkTargetNotices (
    Id             BIGINT IDENTITY(1,1) NOT NULL CONSTRAINT PK_WorkTargetNotices PRIMARY KEY,
    UserId         BIGINT       NOT NULL CONSTRAINT FK_WorkTargetNotices_User REFERENCES agents.Users(Id),
    [Day]          DATE         NOT NULL,
    Kind           VARCHAR(20)  NOT NULL,
    RefAt          DATETIME2    NOT NULL,
    WorkMinutes    INT          NOT NULL,
    TargetMinutes  INT          NOT NULL,
    CreatedAt      DATETIME2    NOT NULL CONSTRAINT DF_WorkTargetNotices_CreatedAt DEFAULT SYSUTCDATETIME(),
    SeenAt         DATETIME2    NULL,
    CONSTRAINT UQ_WorkTargetNotices UNIQUE (UserId, [Day], Kind, RefAt)
);
GO
-- Dashboard alert type 'ClosedEarly': the agent ended the working day before reaching the target. It has no place on
-- the map (the end position is optional) and carries the work time and the target of that moment.
ALTER TABLE agents.WorkAlerts ALTER COLUMN Latitude DECIMAL(9,6) NULL;
GO
ALTER TABLE agents.WorkAlerts ALTER COLUMN Longitude DECIMAL(9,6) NULL;
GO
ALTER TABLE agents.WorkAlerts ADD WorkMinutes INT NULL, TargetMinutes INT NULL;
