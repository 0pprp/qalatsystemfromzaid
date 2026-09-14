namespace BE_Company.Sales.Services;

public enum GlobalManagerPreflightStatus
{
    NotFound = 0,
    ExistingSameGlobalAccount = 1,
    UsernameConflict = 2,
    GlobalIdConflict = 3,
    DuplicateAmbiguous = 4,
    /// <summary>Single active legacy sales-manager row with NULL GlobalAccountId and strict identity match.</summary>
    LegacyAdoptable = 5,
    /// <summary>Username exists but cannot safely adopt (wrong role, disabled, field mismatch, etc.).</summary>
    LegacyConflict = 6,
}

public enum GlobalCreateWritePlan
{
    AdoptOrCreate = 0,
    Conflict = 1,
    BranchUnavailable = 2,
}

public sealed class GlobalManagerCandidate
{
    public int UserId { get; init; }
    public string UserName { get; init; } = "";
    public Guid? GlobalAccountId { get; init; }
    public bool Active { get; init; }
    public string UserType { get; init; } = "";
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    /// <summary>Server-side password equality only. Never serialize to clients/logs.</summary>
    public bool PasswordMatches { get; init; }
}

public sealed record GlobalManagerIdentityRequest
{
    public string? UserName { get; init; }
    public Guid? GlobalAccountId { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? Password { get; init; }
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
    /// <summary>When true, bind legacy NULL GlobalAccountId row instead of inserting.</summary>
    public bool AllowLegacyAdopt { get; init; } = true;
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
        IReadOnlyList<GlobalManagerCandidate> matchesByNameOrGlobal,
        GlobalManagerIdentityRequest request)
    {
        var name = (request.UserName ?? "").Trim();
        var requestedGlobalId = request.GlobalAccountId;
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

        if (byName.Count == 1)
        {
            var row = byName[0];
            if (requestedGlobalId is Guid want)
            {
                if (row.GlobalAccountId is Guid have && have == want)
                {
                    return Same(row);
                }

                if (row.GlobalAccountId is Guid other && other != want)
                {
                    return Conflict(row, GlobalManagerPreflightStatus.UsernameConflict);
                }

                // Legacy: GlobalAccountId IS NULL — username alone is insufficient.
                return EvaluateLegacyAdoption(row, request);
            }

            // Discovery (no GlobalAccountId yet): strict identity only.
            if (row.GlobalAccountId is Guid bound)
            {
                if (!IsStrictSalesManagerIdentity(row, request))
                {
                    return Conflict(row, GlobalManagerPreflightStatus.LegacyConflict);
                }

                return Same(row);
            }

            return EvaluateLegacyAdoption(row, request);
        }

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

    /// <summary>Backward-compatible overload used by older unit tests.</summary>
    public static GlobalManagerPreflightResult EvaluatePreflight(
        IReadOnlyList<(int UserId, string UserName, Guid? GlobalAccountId, bool Active)> matchesByNameOrGlobal,
        string? requestedUserName,
        Guid? requestedGlobalId)
    {
        var mapped = matchesByNameOrGlobal.Select(r => new GlobalManagerCandidate
        {
            UserId = r.UserId,
            UserName = r.UserName,
            GlobalAccountId = r.GlobalAccountId,
            Active = r.Active,
            UserType = ForcedUserType,
            PasswordMatches = false
        }).ToList();

        return EvaluatePreflight(mapped, new GlobalManagerIdentityRequest
        {
            UserName = requestedUserName,
            GlobalAccountId = requestedGlobalId
        });
    }

    private static GlobalManagerPreflightResult EvaluateLegacyAdoption(
        GlobalManagerCandidate row,
        GlobalManagerIdentityRequest request)
    {
        if (row.GlobalAccountId is not null)
        {
            return Conflict(row, GlobalManagerPreflightStatus.UsernameConflict);
        }

        if (!IsStrictSalesManagerIdentity(row, request))
        {
            return Conflict(row, GlobalManagerPreflightStatus.LegacyConflict);
        }

        return new GlobalManagerPreflightResult
        {
            Status = GlobalManagerPreflightStatus.LegacyAdoptable,
            UserId = row.UserId,
            UserName = row.UserName,
            GlobalAccountId = null,
            UserStateActive = row.Active
        };
    }

    private static bool IsStrictSalesManagerIdentity(
        GlobalManagerCandidate row,
        GlobalManagerIdentityRequest request)
    {
        if (!row.Active)
        {
            return false;
        }

        if (!string.Equals(row.UserType?.Trim(), ForcedUserType, StringComparison.Ordinal))
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(request.Password) || !row.PasswordMatches)
        {
            return false;
        }

        return IdentityFieldCompatible(request.Email, row.Email)
               && IdentityFieldCompatible(request.PhoneNumber, row.PhoneNumber);
    }

    /// <summary>
    /// Compatible when either side is blank, or both non-blank and equal (case-insensitive trim).
    /// Conflicting non-empty values block adoption.
    /// </summary>
    public static bool IdentityFieldCompatible(string? requested, string? existing)
    {
        var a = NormalizeField(requested);
        var b = NormalizeField(existing);
        if (a.Length == 0 || b.Length == 0)
        {
            return true;
        }

        return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeField(string? value) => (value ?? "").Trim();

    public static bool CanProceedWithCreate(GlobalManagerPreflightStatus status) =>
        status is GlobalManagerPreflightStatus.NotFound
            or GlobalManagerPreflightStatus.ExistingSameGlobalAccount
            or GlobalManagerPreflightStatus.LegacyAdoptable;

    public static bool CanAdoptLegacy(GlobalManagerPreflightStatus status) =>
        status == GlobalManagerPreflightStatus.LegacyAdoptable;

    public static bool CanProceedWithUpdate(GlobalManagerPreflightStatus status) =>
        status is GlobalManagerPreflightStatus.ExistingSameGlobalAccount
            or GlobalManagerPreflightStatus.NotFound
            or GlobalManagerPreflightStatus.LegacyAdoptable;

    public static GlobalCreateWritePlan DecideCreateWritePlan(IEnumerable<string> branchStatuses)
    {
        var set = branchStatuses
            .Select(s => (s ?? "").Trim())
            .Where(s => s.Length > 0)
            .ToList();

        if (set.Count == 0)
        {
            return GlobalCreateWritePlan.BranchUnavailable;
        }

        foreach (var s in set)
        {
            if (s is "UsernameConflict" or "GlobalIdConflict" or "DuplicateAmbiguous" or "LegacyConflict"
                || s.Equals("LEGACY_CONFLICT", StringComparison.OrdinalIgnoreCase))
            {
                return GlobalCreateWritePlan.Conflict;
            }

            if (s is "BRANCH_UNAVAILABLE" || s.StartsWith("HTTP_", StringComparison.Ordinal))
            {
                return GlobalCreateWritePlan.BranchUnavailable;
            }

            if (s is not ("NotFound" or "ExistingSameGlobalAccount" or "LegacyAdoptable" or "OK"))
            {
                return GlobalCreateWritePlan.Conflict;
            }
        }

        return GlobalCreateWritePlan.AdoptOrCreate;
    }

    public static bool HasConflictingGlobalIds(IEnumerable<Guid?> ids)
    {
        var distinct = ids.Where(g => g.HasValue).Select(g => g!.Value).Distinct().ToList();
        return distinct.Count > 1;
    }

    private static GlobalManagerPreflightResult Same(GlobalManagerCandidate row) =>
        new()
        {
            Status = GlobalManagerPreflightStatus.ExistingSameGlobalAccount,
            UserId = row.UserId,
            UserName = row.UserName,
            GlobalAccountId = row.GlobalAccountId,
            UserStateActive = row.Active
        };

    private static GlobalManagerPreflightResult Conflict(
        GlobalManagerCandidate row,
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
