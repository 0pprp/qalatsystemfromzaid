# Design: دور المدير — تقييمات كل المحافظات (FE_Company)

## Goal
Central web role `المدير` logs in without province, sees province cards, opens tablet-friendly ratings (branch / list / follower / employee) using existing `sales-manager/stats` per city.

## Auth
- Gateway `POST Auth/LoginDirector`
- Config `DirectorAccount` / `DirectorAccounts` (+ bootstrap `حيدر` / `حيدر`)
- JWT UserType=`المدير`, Central=true

## API
- `GET api/director/cities`
- `GET api/director/stats/catalog?cityValue=`
- `GET api/director/stats/trend?cityValue=&scope=&id=&fromDate=&toDate=`
- Proxies via SalesManagerBranchAggregator → BE_Company `sales-manager/stats/*`

## FE
- Login without city tries LoginDirector then LoginSalesManager
- Route `/director-ratings` only for المدير
- Nav item التقييمات
