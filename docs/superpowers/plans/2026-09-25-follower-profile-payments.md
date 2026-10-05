# Plan: Follower customer profile payments + seller + hide sale-request

## Impact
- BE_DelegateWebApplication: CustomerProfile adds SaleName + Payments (View_CustomersPaymentsDelegate)
- follower_application: profile UI payments list, seller name, hideSaleRequest from rating/stats
- No BE_Company / FE_Company / SalesEmployee / DB schema changes

## Done
1. ListCustomerPaymentsAsync (all payments, newest first)
2. SaleName on profile DTO
3. Flutter: تسديدات section, اسم البائع, hideSaleRequest from list-stats + pendingAuth
4. Version 1.0.12+13
