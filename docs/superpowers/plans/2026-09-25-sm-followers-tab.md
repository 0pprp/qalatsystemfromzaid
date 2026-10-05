# Plan: Sales Manager Followers tab

## Impact
- BE_Company: SM followers list/rating/live/route (Sales.Manager policy)
- BE_DelegateWebApplication: official slots = start + end only (not 10-min)
- BE_SalesEmployee: gateway followers routes
- FE_Company: متابعون + موقع مباشر + مسارات under إدارة المبيعات
- follower_application: start/end official pins
- No BE_Company schema migrations; FE_Company only for SM; no FE impact on accountant GPS nav

## Rating
Same as employees: assigned-list customers → CustomerRatingCalculator → chart buckets → customers → profile; back one step via history/dialogs/router.back
