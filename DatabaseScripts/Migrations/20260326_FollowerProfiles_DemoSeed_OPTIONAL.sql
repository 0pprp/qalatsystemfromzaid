-- Optional Demo helper (DO NOT run on Production without review).
-- Creates/updates a Demo follower User + active FollowerProfile.
-- Replace @AsyncId with the password/async the Flutter app will use.

/*
DECLARE @AsyncId NVARCHAR(100) = N'demo-follower-1';
DECLARE @UserName NVARCHAR(100) = N'متابع تجريبي';
DECLARE @CityId INT = NULL; -- NULL = open city (all payment lists)
DECLARE @CityName NVARCHAR(200) = N'النجف - DEMO';
DECLARE @UserId INT;

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE AsyncID = @AsyncId)
BEGIN
    INSERT INTO dbo.Users (UserName, Password, AsyncID, UserType, UserState, CreatedDate)
    VALUES (@UserName, @AsyncId, @AsyncId, N'متابع', 1, GETDATE());
END

SELECT @UserId = UserID FROM dbo.Users WHERE AsyncID = @AsyncId;

IF @UserId IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.FollowerProfiles WHERE UserId = @UserId)
BEGIN
    INSERT INTO dbo.FollowerProfiles (UserId, IsActive, CityId, CityName)
    VALUES (@UserId, 1, @CityId, @CityName);
END
ELSE IF @UserId IS NOT NULL
BEGIN
    UPDATE dbo.FollowerProfiles
    SET IsActive = 1, CityId = @CityId, CityName = @CityName, UpdatedAtUtc = SYSUTCDATETIME()
    WHERE UserId = @UserId;
END
*/
