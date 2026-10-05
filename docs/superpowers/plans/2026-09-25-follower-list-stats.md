# Plan: Follower list stats enrichment

## Impact
- BE_DelegateWebApplication (all branches): enriched ListStats + drill-down
- follower_application Flutter only
- No BE_Company / FE_Company / SalesEmployee / DB schema migrations required (uses existing views)

## Done
1. Rating calculator + active (>3.8%) rules + tests
2. FollowerListStatsService KPIs + rating/active customer endpoints
3. Flutter drill-down: card → buckets → names → profile
4. Deployed Delegate* + published APK 1.0.11+12
