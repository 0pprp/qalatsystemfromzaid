using BE_DelegateWebApplication.DTO;

namespace BE_DelegateWebApplication;

/// <summary>
/// Prevents duplicate SelectDelegate child lists / customers from multiplying
/// rows in the delegate app SQLite sync (قائمة المتوقفين / الغير مسددين).
/// </summary>
public static class DelegateListDedupe
{
    public static IEnumerable<SelectDelegateGetDTO> DistinctByDelegateId(
        IEnumerable<SelectDelegateGetDTO>? rows)
    {
        if (rows == null) yield break;
        var seen = new HashSet<int>();
        foreach (var row in rows)
        {
            var id = row.DelegateId ?? 0;
            if (id <= 0) continue;
            if (!seen.Add(id)) continue;
            yield return row;
        }
    }

    public static IEnumerable<CustomerGetDTO> DistinctByCustomerId(
        IEnumerable<CustomerGetDTO>? rows)
    {
        if (rows == null) yield break;
        var seen = new HashSet<int>();
        foreach (var row in rows)
        {
            if (row.CustomerId <= 0) continue;
            if (!seen.Add(row.CustomerId)) continue;
            yield return row;
        }
    }
}
