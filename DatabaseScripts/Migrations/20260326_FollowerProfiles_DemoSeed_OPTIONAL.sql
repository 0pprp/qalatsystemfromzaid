-- Optional Demo helper (DO NOT run on Production without review).
-- Creates/updates a Demo follower User (UserType = متابع). No FollowerProfiles required.

/*
DECLARE @AsyncId NVARCHAR(100) = N'demo-follower-1';
DECLARE @UserName NVARCHAR(100) = N'متابع تجريبي';
DECLARE @CityId INT = NULL; -- optional city via UsersSelectedCities
DECLARE @CityName NVARCHAR(200) = N'النجف - DEMO';
DECLARE @UserId INT;

IF NOT EXISTS (SELECT 1 FROM dbo.Users WHERE AsyncID = @AsyncId)
BEGIN
    INSERT INTO dbo.Users (UserName, Password, AsyncID, UserType, UserState, CreatedDate)
    VALUES (@UserName, @AsyncId, @AsyncId, N'متابع', 1, GETDATE());
END
ELSE
BEGIN
    UPDATE dbo.Users
    SET UserType = N'متابع',
        UserState = 1,
        UserName = @UserName
    WHERE AsyncID = @AsyncId;
END

SELECT @UserId = UserID FROM dbo.Users WHERE AsyncID = @AsyncId;

-- Optional city assignment (lists scoped by UsersSelectedCities).
IF @UserId IS NOT NULL AND @CityId IS NOT NULL
AND NOT EXISTS (SELECT 1 FROM dbo.UsersSelectedCities WHERE UserID = @UserId AND CityID = @CityId)
BEGIN
    INSERT INTO dbo.UsersSelectedCities (UserID, CityID)
    VALUES (@UserId, @CityId);
END

PRINT CONCAT(N'Demo follower UserId=', @UserId, N' AsyncId=', @AsyncId, N' City=', @CityName);
*/
