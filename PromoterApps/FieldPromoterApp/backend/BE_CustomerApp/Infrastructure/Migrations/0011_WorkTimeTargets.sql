-- v4.9: «المحلات المثبّتة».
-- 1) Where the agent's phone was when a shop was pinned (the pin itself can be moved on the map). NULL for older pins.
ALTER TABLE agents.Prospects ADD AgentLatitude DECIMAL(9,6) NULL, AgentLongitude DECIMAL(9,6) NULL;
GO
-- 2) The daily target is now work time (minutes inside pin periods), not a number of pins. The default is the
--    AppSettings key 'DailyWorkMinutesTarget' (0 = no target); an agent may have their own (NULL = use the default).
--    The old DailyPinTarget column and agents.PinTargetDays stay as history but are no longer used.
ALTER TABLE agents.Users ADD DailyWorkMinutesTarget INT NULL
    CONSTRAINT CK_Users_DailyWorkMinutesTarget CHECK (DailyWorkMinutesTarget BETWEEN 0 AND 1440);
GO
-- One row per agent and finished (Iraq) day, with the target in force that day.
CREATE TABLE agents.WorkTargetDays (
    UserId         BIGINT    NOT NULL CONSTRAINT FK_WorkTargetDays_User REFERENCES agents.Users(Id),
    [Day]          DATE      NOT NULL,
    TargetMinutes  INT       NOT NULL,
    WorkMinutes    INT       NOT NULL,
    Pins           INT       NOT NULL,
    Periods        INT       NOT NULL,
    Worked         BIT       NOT NULL,
    Met            BIT       NOT NULL,
    ClosedAt       DATETIME2 NOT NULL CONSTRAINT DF_WorkTargetDays_ClosedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_WorkTargetDays PRIMARY KEY (UserId, [Day])
);
GO
CREATE INDEX IX_WorkTargetDays_Day ON agents.WorkTargetDays([Day]) INCLUDE (Met, Worked);
GO
CREATE INDEX IX_Prospects_User_Created ON agents.Prospects(CreatedByUserId, CreatedAt) WHERE IsDeleted = 0;
