namespace BE_Company.Sales.Services;

public enum GlobalManagerPreflightStatus
{
    NotFound = 0,
    ExistingSameGlobalAccount = 1,
    UsernameConflict = 2,
    GlobalIdConflict = 3,
    DuplicateAmbiguous = 4,
}

public sealed class GlobalManagerPreflightResult
{
    public GlobalManagerPreflightStatus Status { get; init; }
    public int? UserId { get; init; }
    public Guid? GlobalAccountId { get; init; }
    public string? UserName { get; init; }
    public bool? UserStateActive { get; init; }
}

public sealed class GlobalManagerWriteRequest
{
    public Guid GlobalAccountId { get; init; }
    public string UserName { get; init; } = "";
    public string? Email { get; init; }
    public string? Password { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Address { get; init; }
    public string? UserImage { get; init; }
    public int? ActorUserId { get; init; }
}

public sealed class GlobalManagerWriteResult
{
    public bool Ok { get; init; }
    public string Code { get; init; } = "";
    public int? UserId { get; init; }
    public Guid? GlobalAccountId { get; init; }
    public string? UserName { get; init; }
    public string? Message { get; init; }
}

public static class GlobalSalesManagerRules
{
    public const string ForcedUserType = Authorization.SalesRoles.UserTypeSalesManager;

    public static GlobalManagerPreflightResult EvaluatePreflight(
        IReadOnlyList<(int UserId, string UserName, Guid? GlobalAccountId, bool Active)> matchesByNameOrGlobal,
        string? requestedUserName,
        Guid? requestedGlobalId)
    {
        var name = (requestedUserName ?? "").Trim();
        var byName = matchesByNameOrGlobal
            .Where(r => string.Equals(r.UserName.Trim(), name, StringComparison.OrdinalIgnoreCase))
            .ToList();
        var byGlobal = requestedGlobalId is Guid gid
            ? matchesByNameOrGlobal.Where(r => r.GlobalAccountId == gid).ToList()
            : [];

        if (byName.Count > 1 || byGlobal.Count > 1)
        {
            return new GlobalManagerPreflightResult { Status = GlobalManagerPreflightStatus.DuplicateAmbiguous };
        }

        // Username taken by a different global account (or legacy null id).
        if (byName.Count == 1)
        {
            var row = byName[0];
            if (requestedGlobalId is Guid want)
            {
                if (row.GlobalAccountId is Guid have && have == want)
                {
                    return Same(row);
                }

                return Conflict(row, GlobalManagerPreflightStatus.UsernameConflict);
            }

            return Conflict(row, GlobalManagerPreflightStatus.UsernameConflict);
        }

        // Global id exists (rename / update path).
        if (byGlobal.Count == 1)
        {
            var row = byGlobal[0];
            if (byName.Count == 0)
            {
                return Same(row);
            }
        }

        return new GlobalManagerPreflightResult { Status = GlobalManagerPreflightStatus.NotFound };
    }

    public static bool CanProceedWithCreate(GlobalManagerPreflightStatus status) =>
        status is GlobalManagerPreflightStatus.NotFound
            or GlobalManagerPreflightStatus.ExistingSameGlobalAccount;

    public static bool CanProceedWithUpdate(GlobalManagerPreflightStatus status) =>
        status is GlobalManagerPreflightStatus.ExistingSameGlobalAccount
            or GlobalManagerPreflightStatus.NotFound;

    private static GlobalManagerPreflightResult Same((int UserId, string UserName, Guid? GlobalAccountId, bool Active) row) =>
        new()
        {
            Status = GlobalManagerPreflightStatus.ExistingSameGlobalAccount,
            UserId = row.UserId,
            UserName = row.UserName,
            GlobalAccountId = row.GlobalAccountId,
            UserStateActive = row.Active
        };

    private static GlobalManagerPreflightResult Conflict(
        (int UserId, string UserName, Guid? GlobalAccountId, bool Active) row,
        GlobalManagerPreflightStatus status) =>
        new()
        {
            Status = status,
            UserId = row.UserId,
            UserName = row.UserName,
            GlobalAccountId = row.GlobalAccountId,
            UserStateActive = row.Active
        };
}
