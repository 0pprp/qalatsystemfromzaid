using BE_Company.Sales.Authorization;
using Dapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using System.Data;
using System.Security.Claims;

namespace BE_Company.Sales.Controllers
{
    /// <summary>
    /// Activate/deactivate company Users as Followers (FollowerProfiles). Not Delegates.
    /// </summary>
    [ApiController]
    [Route("api/follower-admin")]
    [Authorize(Policy = SalesPolicies.ReadFollowerGps)]
    public sealed class FollowerAdminController : ControllerBase
    {
        private readonly string _cs;

        public FollowerAdminController(IConfiguration configuration)
        {
            _cs = configuration.GetConnectionString("DataBaseConnection")
                  ?? throw new InvalidOperationException("DataBaseConnection missing.");
        }

        [HttpGet("users")]
        public async Task<IActionResult> SearchUsers([FromQuery] string? q, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var rows = await c.QueryAsync(new CommandDefinition(@"
SELECT TOP 100
    u.UserID AS userId,
    u.UserName AS userName,
    u.UserType AS userType,
    u.AsyncID AS asyncId,
    CAST(CASE WHEN fp.Id IS NOT NULL AND fp.IsActive = 1 THEN 1 ELSE 0 END AS BIT) AS isFollower,
    fp.CityId AS cityId,
    COALESCE(fp.CityName, ci.CityName) AS cityName
FROM dbo.Users u
LEFT JOIN dbo.FollowerProfiles fp ON fp.UserId = u.UserID
LEFT JOIN dbo.Cities ci ON ci.CityID = fp.CityId
WHERE ISNULL(u.UserState, 1) = 1
  AND (@Q IS NULL OR u.UserName LIKE N'%' + @Q + N'%')
ORDER BY u.UserName;",
                new { Q = string.IsNullOrWhiteSpace(q) ? null : q.Trim() }, cancellationToken: ct));
            return Ok(rows);
        }

        [HttpGet("followers")]
        public async Task<IActionResult> ListFollowers(CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var rows = await c.QueryAsync(new CommandDefinition(@"
SELECT
    fp.Id AS profileId,
    u.UserID AS userId,
    u.UserName AS userName,
    u.UserType AS userType,
    u.AsyncID AS asyncId,
    fp.IsActive AS isActive,
    fp.CityId AS cityId,
    COALESCE(fp.CityName, ci.CityName) AS cityName,
    fp.CreatedAtUtc AS createdAtUtc
FROM dbo.FollowerProfiles fp
INNER JOIN dbo.Users u ON u.UserID = fp.UserId
LEFT JOIN dbo.Cities ci ON ci.CityID = fp.CityId
ORDER BY u.UserName;", cancellationToken: ct));
            return Ok(rows);
        }

        public sealed class EnableFollowerBody
        {
            public int UserId { get; set; }
            public int? CityId { get; set; }
            public string? CityName { get; set; }
        }

        [HttpPost("enable")]
        public async Task<IActionResult> Enable([FromBody] EnableFollowerBody body, CancellationToken ct)
        {
            if (body.UserId <= 0) return BadRequest(new { message = "UserId مطلوب" });
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);

            var exists = await c.ExecuteScalarAsync<int?>(new CommandDefinition(
                "SELECT TOP 1 UserID FROM dbo.Users WHERE UserID = @UserId",
                new { body.UserId }, cancellationToken: ct));
            if (exists is null) return NotFound(new { message = "المستخدم غير موجود" });

            var actorId = TryActorUserId();
            var updated = await c.ExecuteAsync(new CommandDefinition(@"
IF EXISTS (SELECT 1 FROM dbo.FollowerProfiles WHERE UserId = @UserId)
BEGIN
    UPDATE dbo.FollowerProfiles
    SET IsActive = 1,
        CityId = COALESCE(@CityId, CityId),
        CityName = COALESCE(@CityName, CityName),
        UpdatedAtUtc = SYSUTCDATETIME()
    WHERE UserId = @UserId;
END
ELSE
BEGIN
    INSERT INTO dbo.FollowerProfiles (UserId, IsActive, CityId, CityName, CreatedByUserId)
    VALUES (@UserId, 1, @CityId, @CityName, @CreatedBy);
END

-- Keep UserType aligned when empty / generic; do not overwrite privileged accountant roles.
UPDATE dbo.Users
SET UserType = N'متابع', UpdatedDate = GETDATE()
WHERE UserID = @UserId
  AND (UserType IS NULL OR UserType = N'' OR UserType = N'متابع');
",
                new
                {
                    body.UserId,
                    body.CityId,
                    body.CityName,
                    CreatedBy = actorId
                }, cancellationToken: ct));

            return Ok(new { ok = true, userId = body.UserId });
        }

        [HttpPost("disable/{userId:int}")]
        public async Task<IActionResult> Disable(int userId, CancellationToken ct)
        {
            await EnsureSchemaAsync(ct);
            await using var c = new SqlConnection(_cs);
            var n = await c.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.FollowerProfiles
SET IsActive = 0, UpdatedAtUtc = SYSUTCDATETIME()
WHERE UserId = @UserId;",
                new { UserId = userId }, cancellationToken: ct));
            if (n == 0) return NotFound(new { message = "لا يوجد ملف متابع لهذا المستخدم" });
            return Ok(new { ok = true, userId });
        }

        private int? TryActorUserId()
        {
            var raw = User.FindFirstValue("UserID")
                      ?? User.FindFirstValue("UserId")
                      ?? User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }

        private async Task EnsureSchemaAsync(CancellationToken ct)
        {
            await using var c = new SqlConnection(_cs);
            await c.ExecuteAsync(new CommandDefinition(@"
IF OBJECT_ID(N'dbo.FollowerProfiles', N'U') IS NULL
BEGIN
    CREATE TABLE dbo.FollowerProfiles (
        Id INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
        UserId INT NOT NULL,
        IsActive BIT NOT NULL CONSTRAINT DF_FollowerProfiles_IsActive DEFAULT(1),
        CityId INT NULL,
        CityName NVARCHAR(200) NULL,
        CreatedAtUtc DATETIME2 NOT NULL CONSTRAINT DF_FollowerProfiles_CreatedAtUtc DEFAULT (SYSUTCDATETIME()),
        CreatedByUserId INT NULL,
        UpdatedAtUtc DATETIME2 NULL,
        CONSTRAINT UQ_FollowerProfiles_UserId UNIQUE (UserId)
    );
END;", cancellationToken: ct));
        }
    }
}
