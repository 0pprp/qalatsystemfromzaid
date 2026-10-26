# Impact: Delegate single list (يوزر المندوب)

## Change
- `BE_DelegateWebApplication` `GetDelegateSelect` returns only the logged-in `DelegateID` row (no SelectDelegate siblings).
- Flutter `delegate_application` removes «اختر القائمة»; auto-loads login `DelegateID`; version `1.0.39+69` force OTA.

## Impact matrix
| Surface | Impact |
|---------|--------|
| BE_Delegate* (Haider) | Yes — list API + child-list ACL via GetDelegateSelect |
| BE_Company / FE_Company | No |
| BE_SalesEmployee | No (OTA table only via publish script) |
| Flutter delegate | Yes |
| Flutter aqeel / follower / BM | No |
| DB schema | No (query change only) |

## Regression notes
- Auth that previously allowed child list IDs via SelectDelegate now allows self only (aligned with one-list-per-user).
- Followers still use `GetFollowerCityLists` / `FollowerUserLists` (unchanged).
