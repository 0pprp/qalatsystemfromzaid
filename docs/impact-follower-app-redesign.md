# Impact: Follower app redesign (cards + collection + declining)

## Scope (in)

| Surface | Change |
|---------|--------|
| `follower_application` | Home list cards, list detail tabs, bottom tabs (الجباية / زبائن متدنية), mini payment UI |
| `BE_DelegateWebApplication` | `Followers/ListCards`, remainder customers, collection lists + follower payment, declining + notes POST, delegate login lock when `FollowerCollectionEnabled` |
| `delegate_application` | Display reject message / logout when login returns collection-locked (minimal) |

## Scope (out)

- No JWT / signing key changes shared with `BE_Company`
- No `FE_ShortCompany`, PDT, `branch_manager_application` UI/API changes
- No real-rating exposure to follower (phantom only, unlabeled)
- Accountant toggle UI already exists (`FE_Company` follower-collection-lists) — untouched except regression

## Regression checklist

- [x] Impact recorded (`docs/impact-follower-app-redesign.md`)
- [x] Unit: trend / decline / collection lock messages (50 BE tests green)
- [x] Flutter follower suite green (69 tests)
- [x] Delegate login surfaces collection-lock Arabic message on 401
- [ ] Manual: deploy BE_Delegate, install follower APK, toggle accountant collection
