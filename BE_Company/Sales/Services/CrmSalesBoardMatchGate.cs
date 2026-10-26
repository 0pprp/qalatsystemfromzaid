using BE_Company.Sales.DTO;

namespace BE_Company.Sales.Services;

/// <summary>
/// CRM auto-assign gate: block when program evaluation OR board duplicate search (طلبات البيع) has a hit.
/// Mirrors program + board criteria (اسم ثلاثي / هاتف / أب وجد / ابن مع أب).
/// </summary>
public static class CrmSalesBoardMatchGate
{
    public static bool ShouldBlockAutoAssign(bool programHasMatch, bool boardHasMatch) =>
        programHasMatch || boardHasMatch;

    public static bool HasBoardDuplicate(SalesRequestDTO self, IEnumerable<SalesRequestDTO> peers)
    {
        ArgumentNullException.ThrowIfNull(self);
        foreach (var peer in peers)
        {
            if (peer is null || peer.Id == self.Id)
            {
                continue;
            }

            if (SalesPhoneNormalizer.Matches(self.CustomerPhone, peer.CustomerPhone))
            {
                return true;
            }

            if (SalesRequestNameSimilarity.IsTripleNameMatch(self.CustomerName, peer.CustomerName))
            {
                return true;
            }

            if (SalesRequestNameSimilarity.IsFatherOrGrandfatherMatch(self.CustomerName, peer.CustomerName))
            {
                return true;
            }

            if (SalesRequestNameSimilarity.IsSonFatherMatch(self.CustomerName, peer.CustomerName))
            {
                return true;
            }
        }

        return false;
    }
}
