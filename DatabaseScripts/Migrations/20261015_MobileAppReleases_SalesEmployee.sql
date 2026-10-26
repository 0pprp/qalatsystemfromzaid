-- Seed / upsert sales-employee mobile release row (gateway dbo.MobileAppReleases).
IF OBJECT_ID(N'dbo.MobileAppReleases', N'U') IS NULL
BEGIN
    RAISERROR(N'MobileAppReleases table missing — run prior mobile-updates migration first.', 16, 1);
    RETURN;
END
GO

MERGE dbo.MobileAppReleases AS t
USING (SELECT N'sales-employee' AS AppKey) AS s
ON t.AppKey = s.AppKey
WHEN MATCHED THEN
    UPDATE SET
        LatestVersionName = COALESCE(NULLIF(t.LatestVersionName, N''), N'1.0.0'),
        LatestVersionCode = CASE WHEN t.LatestVersionCode < 1 THEN 1 ELSE t.LatestVersionCode END,
        MinimumSupportedVersionCode = CASE WHEN t.MinimumSupportedVersionCode < 1 THEN 1 ELSE t.MinimumSupportedVersionCode END,
        UpdatedAtUtc = SYSUTCDATETIME()
WHEN NOT MATCHED THEN
    INSERT (AppKey, LatestVersionName, LatestVersionCode, MinimumSupportedVersionCode, ForceUpdate, ApkUrl, Sha256, ReleaseNotes, UpdatedAtUtc)
    VALUES (
        N'sales-employee',
        N'1.0.0',
        1,
        1,
        0,
        N'',
        N'',
        N'تطبيق موظف المبيعات',
        SYSUTCDATETIME()
    );
GO
