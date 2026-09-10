-- OPTIONAL — run ONLY after confirming no runtime code or reports use dbo.FollowerProfiles.
-- Do NOT run on Production without backup + approval.
-- Prefer keeping the table unused for compatibility until verified.

/*
IF OBJECT_ID(N'dbo.FollowerProfiles', N'U') IS NOT NULL
BEGIN
    -- Safety: refuse drop when rows still exist (review/archive first).
    IF EXISTS (SELECT 1 FROM dbo.FollowerProfiles)
    BEGIN
        RAISERROR(N'FollowerProfiles still has rows. Archive/migrate then re-run.', 16, 1);
        RETURN;
    END

    DROP TABLE dbo.FollowerProfiles;
    PRINT N'dbo.FollowerProfiles dropped.';
END
ELSE
BEGIN
    PRINT N'dbo.FollowerProfiles already absent.';
END
*/
