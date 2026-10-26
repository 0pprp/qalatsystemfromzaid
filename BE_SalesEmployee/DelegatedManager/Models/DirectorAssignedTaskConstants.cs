namespace BE_SalesEmployee.DelegatedManager.Models;

public static class DirectorAssignedTaskStatus
{
    public const string Sent = "مرسلة";
    public const string Transferred = "منقولة";
    public const string Accepted = "مقبولة";
    public const string Returned = "معادة";
    public const string AwaitingDelegatedConfirm = "بانتظار_تصديق_المفوض";
    public const string Completed = "منجزة";
}

public static class DirectorAssignedTaskPriority
{
    /// <summary>عالية</summary>
    public const byte High = 1;

    /// <summary>متوسطة</summary>
    public const byte Medium = 2;

    /// <summary>ضعيفة</summary>
    public const byte Low = 3;
}

public static class DirectorAssignedTaskAssigneeKind
{
    public const string DelegatedManager = "DelegatedManager";
    public const string BranchManager = "BranchManager";
    /// <summary>مسؤول اللجنة الميدانية (متابع) — key = "{cityValue}|{userId}".</summary>
    public const string FieldCommitteeFollower = "FieldCommitteeFollower";
}
