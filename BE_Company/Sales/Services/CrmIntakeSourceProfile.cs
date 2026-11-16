namespace BE_Company.Sales.Services;

/// <summary>
/// Who a gateway intake row is attributed to. The CRM_OF_ALDHAMAN path stays the default;
/// Source="CustomerApp" (BE_CustomerApp) gets its own source type and is never relayed to the old CRM.
/// </summary>
public sealed class CrmIntakeSourceProfile
{
    public string CustomerSourceType { get; init; } = SalesRequestSources.Crm;
    public string CreatedByName { get; init; } = "قسم التسويق";
    public string CreatedByUserType { get; init; } = SalesRequestSources.Crm;
    /// <summary>Label used when productName is appended to Notes.</summary>
    public string ProductNoteLabel { get; init; } = "منتج CRM";
    /// <summary>True only for real CRM rows: the board-status forwarder must stay silent for others.</summary>
    public bool NotifyCrmBoard { get; init; } = true;
    /// <summary>
    /// False for every gateway source (قسم التسويق / CRM and the agents' app): requests stay in
    /// «غير المسندة» for the sales manager to examine and assign. Kept as a switch in case a source needs round-robin again.
    /// </summary>
    public bool AutoAssign { get; init; } = false;

    public static readonly CrmIntakeSourceProfile Crm = new();

    public static readonly CrmIntakeSourceProfile CustomerApp = new()
    {
        CustomerSourceType = SalesRequestSources.CustomerApp,
        CreatedByName = SalesRequestSources.CustomerAppArabicLabel,
        CreatedByUserType = SalesRequestSources.CustomerApp,
        ProductNoteLabel = "منتج تطبيق المروّجين",
        NotifyCrmBoard = false,
        AutoAssign = false
    };

    /// <summary>Name stored on the request (CreatedByName): the agent's name for app orders, shown as «المروج: name».</summary>
    public string CreatedByNameFor(string? submittedByName)
    {
        var name = submittedByName?.Trim();
        if (!SalesRequestSources.IsCustomerApp(CustomerSourceType) || string.IsNullOrWhiteSpace(name))
        {
            return CreatedByName;
        }

        return name.Length > 100 ? name[..100] : name;
    }

    /// <summary>Unknown / empty sources fall back to CRM (backward compatible with the existing CRM caller).</summary>
    public static CrmIntakeSourceProfile Resolve(string? source) =>
        SalesRequestSources.IsCustomerApp(source?.Trim()) ? CustomerApp : Crm;
}
