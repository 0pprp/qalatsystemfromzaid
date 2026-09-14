using Dapper;
using Microsoft.Data.SqlClient;

namespace BE_Company.Sales.Services;

public interface IGlobalSalesManagerRepository
{
    Task EnsureSchemaAsync(CancellationToken ct = default);
    Task<IReadOnlyList<GlobalManagerCandidate>> FindCandidatesAsync(
        GlobalManagerIdentityRequest identity,
        CancellationToken ct = default);
    Task<GlobalManagerPreflightResult> PreflightAsync(
        GlobalManagerIdentityRequest identity,
        CancellationToken ct = default);
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
        await connection.ExecuteAsync(new CommandDefinition(
            GlobalAccountIdSchemaSql.EnsureScript,
            cancellationToken: ct));
        Volatile.Write(ref _schemaReady, 1);
    }

    public async Task<IReadOnlyList<GlobalManagerCandidate>> FindCandidatesAsync(
        GlobalManagerIdentityRequest identity,
        CancellationToken ct = default)
    {
        await EnsureSchemaAsync(ct);
        var name = (identity.UserName ?? "").Trim();
        await using var connection = new SqlConnection(_connectionString);
        var rows = await connection.QueryAsync(new CommandDefinition(@"
SELECT UserID, UserName, GlobalAccountId, UserState, UserType, Email, PhoneNumber, Password
FROM dbo.Users
WHERE (@UserName <> N'' AND LOWER(LTRIM(RTRIM(UserName))) = LOWER(@UserName))
   OR (@GlobalAccountId IS NOT NULL AND GlobalAccountId = @GlobalAccountId)
", new { UserName = name, GlobalAccountId = identity.GlobalAccountId }, cancellationToken: ct));

        var list = new List<GlobalManagerCandidate>();
        var suppliedPassword = identity.Password ?? "";
        foreach (var r in rows)
        {
            var storedPassword = (string?)(r.Password) ?? "";
            list.Add(new GlobalManagerCandidate
            {
                UserId = (int)r.UserID,
                UserName = (string)(r.UserName ?? ""),
                GlobalAccountId = (Guid?)r.GlobalAccountId,
                Active = UserLoginDiagnostics.IsActiveUserState(r.UserState),
                UserType = (string)(r.UserType ?? ""),
                Email = (string?)r.Email,
                PhoneNumber = (string?)r.PhoneNumber,
                PasswordMatches = string.Equals(storedPassword, suppliedPassword, StringComparison.Ordinal)
            });
        }

        return list;
    }

    public async Task<GlobalManagerPreflightResult> PreflightAsync(
        GlobalManagerIdentityRequest identity,
        CancellationToken ct = default)
    {
        var matches = await FindCandidatesAsync(identity, ct);
        return GlobalSalesManagerRules.EvaluatePreflight(matches, identity);
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

        var identity = new GlobalManagerIdentityRequest
        {
            UserName = userName,
            GlobalAccountId = request.GlobalAccountId,
            Email = request.Email,
            PhoneNumber = request.PhoneNumber,
            Password = request.Password
        };

        var preflight = await PreflightAsync(identity, ct);
        if (!GlobalSalesManagerRules.CanProceedWithCreate(preflight.Status)
            && !GlobalSalesManagerRules.CanProceedWithUpdate(preflight.Status))
        {
            var code = preflight.Status == GlobalManagerPreflightStatus.LegacyConflict
                ? "LEGACY_CONFLICT"
                : preflight.Status.ToString().ToUpperInvariant();
            return Fail(code, "تعارض في الحساب.");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(ct);

        if (preflight.Status is GlobalManagerPreflightStatus.ExistingSameGlobalAccount
                or GlobalManagerPreflightStatus.LegacyAdoptable
            && preflight.UserId is int existingId)
        {
            // Never auto-enable UserState. Adoption binds GlobalAccountId only + optional field sync.
            await connection.ExecuteAsync(new CommandDefinition(@"
UPDATE dbo.Users
SET UserName = @UserName,
    Email = COALESCE(@Email, Email),
    Password = CASE WHEN @Password IS NULL OR @Password = N'' THEN Password ELSE @Password END,
    PhoneNumber = COALESCE(@PhoneNumber, PhoneNumber),
    Address = COALESCE(@Address, Address),
    UserImage = COALESCE(@UserImage, UserImage),
    UserType = @UserType,
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
                Code = preflight.Status == GlobalManagerPreflightStatus.LegacyAdoptable ? "ADOPTED" : "UPDATED",
                UserId = existingId,
                GlobalAccountId = request.GlobalAccountId,
                UserName = userName
            };
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return Fail("PASSWORD_REQUIRED", "كلمة المرور مطلوبة عند الإنشاء.");
        }

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
