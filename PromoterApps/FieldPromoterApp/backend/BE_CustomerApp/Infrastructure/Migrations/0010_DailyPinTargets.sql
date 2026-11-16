-- Daily target of pinned shops. The default is the AppSettings key 'DailyPinTarget' (0 = no target); an agent can have
-- their own target (NULL = use the default).
ALTER TABLE agents.Users ADD DailyPinTarget INT NULL CONSTRAINT CK_Users_DailyPinTarget CHECK (DailyPinTarget BETWEEN 0 AND 100);
GO
-- One row per agent and finished (Iraq) day, written after the day ends with the target in force then, so later
-- changes of the target do not rewrite history. Worked = had a work session or pinned at least one shop.
CREATE TABLE agents.PinTargetDays (
    UserId    BIGINT    NOT NULL CONSTRAINT FK_PinTargetDays_User REFERENCES agents.Users(Id),
    [Day]     DATE      NOT NULL,
    Target    INT       NOT NULL,
    Pins      INT       NOT NULL,
    Worked    BIT       NOT NULL,
    Met       BIT       NOT NULL,
    ClosedAt  DATETIME2 NOT NULL CONSTRAINT DF_PinTargetDays_ClosedAt DEFAULT SYSUTCDATETIME(),
    CONSTRAINT PK_PinTargetDays PRIMARY KEY (UserId, [Day])
);
GO
CREATE INDEX IX_PinTargetDays_Day ON agents.PinTargetDays([Day]) INCLUDE (Met, Worked);
