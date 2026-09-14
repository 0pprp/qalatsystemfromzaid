namespace BE_Company.Sales.Authorization;

/// <summary>
/// Server-side whitelist: which actor UserType may assign which target UserType on create/update.
/// Arabic literals match dbo.Users.UserType / JWT claim "UserType".
/// </summary>
public static class UserCreationAuthorization
{
    /// <summary>
    /// Types the main accountant may create or assign. Includes Sales Manager.
    /// Does not grant Sales Manager any elevated admin powers — only the stored UserType value.
    /// </summary>
    private static readonly HashSet<string> MainAccountantAssignableTypes = new(StringComparer.Ordinal)
    {
        SalesRoles.UserTypeMainAccountant,
        SalesRoles.UserTypeSubAccountant,
        SalesRoles.UserTypeBranchManager,
        SalesRoles.UserTypeSalesEmployee,
        SalesRoles.UserTypeSalesFilterEmployee,
        SalesRoles.UserTypeFollower,
        SalesRoles.UserTypeSalesManager,
    };

    public static bool CanAssignUserType(string? actorUserType, string? targetUserType)
    {
        if (string.IsNullOrWhiteSpace(actorUserType) || string.IsNullOrWhiteSpace(targetUserType))
        {
            return false;
        }

        var target = targetUserType.Trim();

        // Only المحاسب الرئيسي may manage company users from this panel.
        if (!string.Equals(actorUserType.Trim(), SalesRoles.UserTypeMainAccountant, StringComparison.Ordinal))
        {
            return false;
        }

        // Follower variants historically stored as "متابع..." — allow exact متابع or prefix.
        if (SalesRoles.IsFollower(target))
        {
            return true;
        }

        return MainAccountantAssignableTypes.Contains(target);
    }

    public static IReadOnlyList<string> AssignableTypesFor(string? actorUserType)
    {
        if (!string.Equals(actorUserType?.Trim(), SalesRoles.UserTypeMainAccountant, StringComparison.Ordinal))
        {
            return Array.Empty<string>();
        }

        return MainAccountantAssignableTypes.OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }
}
