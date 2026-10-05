namespace BE_DelegateWebApplication.Services;

/// <summary>
/// When accountant enables follower collection on a list, delegate device intake is locked.
/// </summary>
public static class FollowerCollectionPaymentGate
{
    public const string LockedMessage =
        "جباية هذه القائمة مفعّلة للمتابعين — التسديد من تطبيق المندوب مقفل";

    /// <summary>
    /// Lock when the customer's list OR the authenticated collector list has FollowerCollectionEnabled.
    /// </summary>
    public static string GateSql => @"
IF COL_LENGTH(N'dbo.Delegates', N'FollowerCollectionEnabled') IS NULL
BEGIN
    SELECT CAST(0 AS BIT);
    RETURN;
END
SELECT CAST(CASE
    WHEN EXISTS (
        SELECT 1 FROM dbo.Delegates d
        WHERE d.DelegateID IN (@CustomerListId, @AuthDelegateId)
          AND ISNULL(d.FollowerCollectionEnabled, 0) = 1
    ) THEN 1 ELSE 0 END AS BIT);";
}
