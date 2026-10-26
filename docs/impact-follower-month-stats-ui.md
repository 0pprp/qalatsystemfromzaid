# Impact: Follower month/week ratings + stats UI + ListCards perf

## Scope (in)

| Surface | Change |
|---------|--------|
| `BE_DelegateWebApplication` | `ListCards`: `ScoreMonthAgo`/`MonthTrend`; fast View_Customers path + parallel lists; `ListStats`: week/month prior scores + trends; daily rating snapshot upsert |
| `follower_application` | Hide field-followups drawer; home month trend card + cache-first; stats follower summary + 4 ratings/list; remove 3.8% active block |

## Scope (out)

- No FE_Company / BE_Company / Flutter other apps
- No Manhaj/Aqeel Delegate runtime deploy
- No OTA / mandatory app update publish (USB install only)

## Regression

- [x] BE unit: FollowerList* (29 passed)
- [x] Flutter analyze (changed files; info-only prefer_const)
- [x] USB install follower APK on device
- [x] Deploy BE_Delegate (non-Manhaj): 14 updated, Manhaj/Aqeel skipped, services restarted
