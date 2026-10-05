# Sales requests duplicate search (FE)

## Goal
Let sales managers find duplicate / previously rejected names among sales requests, and locate any request by name or phone among hundreds/thousands of cards.

## Scope
FE_Company `sales-manager-requests.vue` only. No BE API change for v1 (list already loads all requests for selected city / all cities).

## Behavior
1. **Toolbar search** beside province filter: free-text name or phone; filters list to matching request cards regardless of status tab; shows full card + status chips.
2. **Under التقييم**: new block «بحث داخل طلبات البيع» with triple / phone / father+grandfather match against other loaded requests (exclude self); show matching cards with status + details. Existing customer-catalog evaluation unchanged.

## Matching
Port BE `SalesRequestNameSimilarity` + digit-only phone compare into `salesRequestDuplicateSearch.js`.
