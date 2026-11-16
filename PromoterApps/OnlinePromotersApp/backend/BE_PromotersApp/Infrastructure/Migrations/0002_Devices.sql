-- Phones that receive push notifications (Firebase Cloud Messaging). A token belongs to whoever signed in last on it.
CREATE TABLE promoters.Devices (
    Id         BIGINT IDENTITY(1,1) PRIMARY KEY,
    UserId     BIGINT        NOT NULL REFERENCES promoters.Users(Id),
    Token      VARCHAR(512)  NOT NULL,
    Platform   VARCHAR(10)   NOT NULL,
    AppVersion VARCHAR(30)   NULL,
    CreatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME(),
    UpdatedAt  DATETIME2     NOT NULL DEFAULT SYSUTCDATETIME()
);
CREATE UNIQUE INDEX UX_Devices_Token ON promoters.Devices(Token);
CREATE INDEX IX_Devices_User ON promoters.Devices(UserId);
