using BE_Company.DTO;
using Dapper;
using Microsoft.Data.SqlClient;
using System.Data;

namespace BE_Company.Sales.Services;

public interface IGlobalSalesManagerRepository
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<(int UserId, string UserName, Guid? GlobalAccountId, bool Active)>> FindCandidatesAsync(
        string? userName,
        Guid? globalAccountId,
        CancellationToken ct = default);
    Task<GlobalManagerPreflightResult> PreflightAsync(string? userName, Guid? globalAccountId, CancellationToken ct = default);
    Task<GlobalManagerWriteResult> UpsertAsync(GlobalManagerWriteRequest request, CancellationToken ct = default);
    Task<GlobalManagerWriteResult> DisableAsync(Guid globalAccountId, CancellationToken ct = default);
}

public sealed class GlobalSalesManagerRepository : IGlobalSalesManagerRepository
{
    private readonly string _connectionString;
    private int _schemaReady;

    public GlobalSalesManagerRepository(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("DataBaseConnection")
            ?? throw new InvalidOperationException("DataBaseConnection missing.");
    }

    public async Task EnsureSchemaAsync(CancellationToken ct = default)
    {
        if (Volatile.Read(ref _schemaReady) == 1)
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.ExecuteAsync(new CommandDefinition(@"
IF COL_LENGTH(N'dbo.Users', N'GlobalAccountId') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD GlobalAccountId UNIQUEIDENTIFIER NULL;
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_GlobalAccountId' AND object_id = OBJECT_ID(N'dbo.Users'))
BEGIN
    CREATE NONCLUSTERED INDEX IX_Users_GlobalAccountId
        ON dbo.Users(GlobalAccountId)
        WHERE GlobalAccountId IS NOT NULL;
END
", cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
    }

    public async Task<IReadOnlyList<(int UserId, string UserName, Guid? GlobalAccountId, bool Active)>> FindCandidatesAsync(
        string? userName,
        Guid? globalAccountId,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var name = (userName ?? "").Trim();
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync(new CommandDefinition(@"
SELECT UserID, UserName, GlobalAccountId, UserState
FROM dbo.Users
WHERE (@UserName <> N'' AND LOWER(LTRIM(RTRIM(UserName))) = LOWER(@UserName))
   OR (@GlobalAccountId IS NOT NULL AND GlobalAccountId = @GlobalAccountId)
", new { UserName = name, GlobalAccountId = globalAccountId }, cancellationToken: ct));

        var list = new List<(int UserId, string UserName, Guid? GlobalAccountId, bool Active)>();
        foreach (var r in rows)
        {
            list.Add((
                (int)r.UserID,
                (string)(r.UserName ?? ""),
                (Guid?)r.GlobalAccountId,
                UserLoginDiagnostics.IsActiveUserState(r.UserState)
            ));
        }

        return list;
    }

    public async Task<GlobalManagerPreflightResult> PreflightAsync(
        string? userName,
        Guid? globalAccountId,
        CancellationToken ct = default)
    {
        var matches = await FindCandidatesAsync(userName, globalAccountId, ct);
        return GlobalSalesManagerRules.EvaluatePreflight(matches, userName, globalAccountId);
    }

    public async Task<GlobalManagerWriteResult> UpsertAsync(GlobalManagerWriteRequest request, CancellationToken ct = default)
    {
        if (request.GlobalAccountId == Guid.Empty)
        {
            return Fail("INVALID_GLOBAL_ID", "GlobalAccountId مطلوب.");
        }

        var userName = (request.UserName ?? "").Trim();
        if (userName.Length == 0)
        {
            return Fail("INVALID_USERNAME", "اسم المستخدم مطلوب.");
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            // Password required on create; optional on update when empty means keep.
        }

        var preflight = await PreflightAsync(userName, request.GlobalAccountId, ct);
        if (!GlobalSalesManagerRules.CanProceedWithCreate(preflight.Status)
            && preflight.Status != GlobalManagerPreflightStatus.ExistingSameGlobalAccount)
        {
            return Fail(preflight.Status.ToString().ToUpperInvariant(), "تعارض في الحساب.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        if (preflight.Status == GlobalManagerPreflightStatus.ExistingSameGlobalAccount && preflight.UserId is int existingId)
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.Users
SET UserName = @UserName,
    Email = COALESCE(@Email, Email),
    Password = CASE WHEN @Password IS NULL OR @Password = N'' THEN Password ELSE @Password END,
    PhoneNumber = COALESCE(@PhoneNumber, PhoneNumber),
    Address = COALESCE(@Address, Address),
    UserImage = COALESCE(@UserImage, UserImage),
    UserType = @UserType,
    UserState = 1,
    GlobalAccountId = @GlobalAccountId
WHERE UserID = @UserID
", new
            {
                UserID = existingId,
                UserName = userName,
                request.Email,
                request.Password,
                request.PhoneNumber,
                request.Address,
                request.UserImage,
                UserType = GlobalSalesManagerRules.ForcedUserType,
                request.GlobalAccountId
            }, cancellationToken: ct));

            return new GlobalManagerWriteResult
            {
                Ok = true,
                Code = "UPDATED",
                UserId = existingId,
                GlobalAccountId = request.GlobalAccountId,
                UserName = userName
            };
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Fail("PASSWORD_REQUIRED", "كلمة المرور مطلوبة عند الإنشاء.");
        }

        // Prefer explicit insert so GlobalAccountId is set (legacy Users_Create SP has no column).
        var newId = await connection.ExecuteScalarAsync<int>(new CommandDefinition(@"
INSERT INTO dbo.Users
    (UserName, Email, Password, PhoneNumber, Address, UserState, AsyncID, AsyncState, UserType, UserImage, GlobalAccountId)
VALUES
    (@UserName, @Email, @Password, @PhoneNumber, @Address, 1, NEWID(), 0, @UserType, @UserImage, @GlobalAccountId);
SELECT CAST(SCOPE_IDENTITY() AS INT);
", new
        {
            UserName = userName,
            request.Email,
            request.Password,
            request.PhoneNumber,
            request.Address,
            UserType = GlobalSalesManagerRules.ForcedUserType,
            request.UserImage,
            request.GlobalAccountId
        }, cancellationToken: ct));

        if (request.ActorUserId is int actorId)
        {
            await connection.ExecuteAsync(new CommandDefinition(@"
INSERT INTO dbo.Activities (UserID, ActivityDescription, ActivityDate, AsyncState, AsyncID)
VALUES (@ActorId, @Desc, GETUTCDATE(), 'false', NEWID())
", new
            {
                ActorId = actorId,
                Desc = "تم اضافة المستخدم  " + userName
            }, cancellationToken: ct));
        }

        return new GlobalManagerWriteResult
        {
            Ok = true,
            Code = "CREATED",
            UserId = newId,
            GlobalAccountId = request.GlobalAccountId,
            UserName = userName
        };
    }

    public async Task<GlobalManagerWriteResult> DisableAsync(Guid globalAccountId, CancellationToken ct = default)
    {
        if (globalAccountId == Guid.Empty)
        {
            return Fail("INVALID_GLOBAL_ID", "GlobalAccountId مطلوب.");
        }

        await EnsureSchemaAsync(ct);
        await using var connection = new SqlConnection(_connectionString);
        var affected = await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.Users
SET UserState = 0
WHERE GlobalAccountId = @GlobalAccountId
", new { GlobalAccountId = globalAccountId }, cancellationToken: ct));

        if (affected == 0)
        {
            return Fail("NOT_FOUND", "الحساب غير موجود.");
        }

        return new GlobalManagerWriteResult
        {
            Ok = true,
            Code = "DISABLED",
            GlobalAccountId = globalAccountId
        };
    }

    private static GlobalManagerWriteResult Fail(string code, string message) =>
        new() { Ok = false, Code = code, Message = message };
}
