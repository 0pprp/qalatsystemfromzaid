# Impact: Director ملخص tab

## Change
Cross-branch day×province financial matrix for المدير (حيدر) on AdminFrontend.

### المبلغ الكلي (per branch)
`خزينة المحافظة` (or `خزينة {المدينة}`) + `قاصة/خزينة ابو سجاد` (not `دين ابو سجاد`) + sum of active list boxes (`Delegates.BoxID` / `View_Box`) where any customer paid within 30 days. Inactive list boxes excluded. Special boxes not double-counted.

## Surfaces
| Surface | Impact |
|---------|--------|
| BE_Company | Yes — `sales-manager/director-summary/*` |
| BE_SalesEmployee | Yes — `api/director/summary/*` Qalaa fan-out |
| FE_Company / AdminFrontend | Yes — tab ملخص |
| BE_Delegate / Flutter / FE_Short | No |
| DB schema | No |

## Related: Cancel → غير مسند
`POST sales-manager/sales-requests/{id}/cancel` (+ gateway `{city}/{id}/cancel`): any status except `Rejected`; voids sold via cascade; resets to `New` / no assignee; history `CancelledToUnassigned`; employee app hides automatically.

## Qalaa cities
IDs 1–6, 8–14. Excludes 7/15/16/18 (كوت، عقيل، ميسان).
