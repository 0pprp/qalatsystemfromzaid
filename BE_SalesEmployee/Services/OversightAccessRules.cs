namespace BE_SalesEmployee.Services;

/// <summary>
/// Who may create which oversight account, and the username/password rules for those accounts.
/// Usernames may be Arabic. Passwords have no length or character rules beyond being non-empty.
/// </summary>
public static class OversightAccessRules
{
    public const string RoleDirector = "المدير";
    public const string RoleSupervisor = "مسؤول المراقبين";
    public const string RoleCommitteeHead = "مسؤول لجنة المراقبة";
    public const string RoleCommitteeAssistant = "مساعد مسؤول لجنة المراقبة";
    public const string RoleArchiveManager = "مسؤول الارشيف";
    public const string RoleLegalAccountant = "محاسب القانونية";
    public const string RoleLegalTeam = "مدير القانونية";

    public static bool IsKnownRole(string? role) =>
        role is RoleDirector or RoleSupervisor or RoleCommitteeHead or RoleCommitteeAssistant or RoleArchiveManager
            or RoleLegalAccountant or RoleLegalTeam;

    public static bool IsCommitteeRole(string? role) =>
        role is RoleCommitteeHead or RoleCommitteeAssistant;

    public static bool CanCreate(string? actorRole, string? targetRole)
    {
        if (!IsKnownRole(targetRole))
            return false;

        if (actorRole == RoleDirector)
            return true;

        if (actorRole is RoleSupervisor or "المراقب")
            return IsCommitteeRole(targetRole);

        return false;
    }

    public static bool CanManageCommittees(string? actorRole) =>
        actorRole is RoleDirector or RoleSupervisor or "المراقب";

    public static string? ValidateUserName(string? userName)
    {
        if (string.IsNullOrWhiteSpace(userName))
            return "اسم المستخدم مطلوب";
        if (userName.Trim().Length > 200)
            return "اسم المستخدم طويل جداً";
        return null;
    }

    public static string? ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Trim().Length == 0)
            return "كلمة المرور مطلوبة";
        if (password.Length > 500)
            return "كلمة المرور طويلة جداً";
        return null;
    }
}
