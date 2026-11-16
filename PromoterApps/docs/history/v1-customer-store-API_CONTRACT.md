# BE_CustomerApp — API contract (v1)

Single source of truth for the mobile app (`mobile/customer_app`), the web store (`web/store`)
and the dashboard pages added to `FE_Company`. All three talk ONLY to `BE_CustomerApp`.

- Base URL: `{API}/api/v1/` (prod e.g. `https://api.qalaa-store.iq/api/v1/`, local `http://localhost:5380/api/v1/`).
- JSON, camelCase. Dates: ISO-8601 UTC strings (`2026-11-03T10:15:00Z`). Money: integer IQD (`long`), never decimals.
- Errors: HTTP 4xx/5xx with body `{ "error": "<Arabic user-facing message>", "code": "<machine_code>" }`.
  Validation errors: 400 `{ "error": "...", "code": "validation", "fields": { "firstName": "مطلوب" } }`.
- Paging: query `page` (1-based), `pageSize` (default 20, max 100). Response `{ "items": [...], "total": 123, "page": 1, "pageSize": 20 }`.
- Customer auth: `Authorization: Bearer <accessToken>` (issued by this API).
- Dashboard auth: `Authorization: Bearer <gateway JWT>` — the same token FE_Company already stores in
  `localStorage.Token` after central login. Accepted `UserType` claims: `المدير`, `مدير مبيعات`.
- Phone numbers: Iraqi mobiles only. Canonical form `07XXXXXXXXX` (11 digits, regex `^07[3-9]\d{8}$`).
  The API also accepts `+9647...`, `9647...`, `7XXXXXXXXX` and normalises. Anything else → 400 `invalid_phone`.

## Enums

| Enum | Values |
|---|---|
| `purchaseType` | `Cash` (نقد), `DailyInstallments` (أقساط يومية — **للمحلات التجارية فقط**) |
| `orderStatus` | `New` طلب جديد · `UnderReview` قيد المعالجة · `Denied` مرفوض · `Transferred` محوّل إلى مندوب المبيعات · `FieldInspection` قيد الكشف الميداني · `Sold` تم البيع · `NotSold` لم يتم البيع · `Cancelled` ملغى |
| `commissionStatus` | `Pending` معلقة · `Available` مستحقة للصرف · `Paid` مصروفة · `Reversed` ملغاة · `Frozen` مجمدة |
| `commissionKind` | `Direct` (level 0) · `Network` (level ≥ 1) · `Clawback` (negative, reversal after payout) · `Adjustment` |
| `payoutStatus` | `Requested` · `Approved` · `Paid` · `Rejected` |
| `notificationType` | `Order` · `Commission` · `Promotion` · `Network` · `System` |

Arabic labels above are the ones to show in UIs (`orderStatusLabel` is also returned by the API).

---

## 1. Public (guest) endpoints — no auth

### `GET branches`
Branches (governorates) the company sells in (Qalaa branches only).
```json
[{ "cityValue": 1, "name": "النجف" }, { "cityValue": 2, "name": "بغداد - الرصافة" }]
```

### `GET catalog/categories`
```json
[{ "id": 3, "name": "أجهزة كهربائية", "iconUrl": "https://.../media/x.png", "sortOrder": 1, "productCount": 12 }]
```

### `GET catalog/banners`
Home banners/offers.
```json
[{ "id": 1, "title": "عرض الشتاء", "subtitle": "...", "imageUrl": "...", "productId": 10, "categoryId": null, "externalUrl": null }]
```

### `GET catalog/products?q=&categoryId=&cityValue=&purchaseType=&sort=&page=&pageSize=`
`sort`: `newest` (default) | `priceAsc` | `priceDesc` | `popular` | `commissionDesc`. `cityValue` filters to products available in that branch.
Response is paged `ProductCard`:
```json
{
  "id": 10, "name": "ثلاجة 18 قدم", "categoryId": 3, "categoryName": "أجهزة كهربائية",
  "thumbnailUrl": "https://.../media/abc.jpg",
  "cashPrice": 650000, "installmentPrice": 780000, "dailyInstallment": 6500, "installmentDays": 120,
  "allowCash": true, "allowInstallments": true, "isFeatured": true, "badge": "جديد",
  "baseCommission": 12000
}
```

### `GET catalog/products/{id}`
`ProductDetail` = `ProductCard` +
```json
{
  "description": "نص حر متعدد الأسطر",
  "media": [{ "type": "image", "url": "..." }, { "type": "video", "url": "https://.../media/v.mp4" }, { "type": "youtube", "url": "https://youtu.be/..." }],
  "specs": [{ "name": "السعة", "value": "18 قدم" }],
  "extraInfo": "معلومات إضافية تحددها الشركة",
  "availableBranches": [{ "cityValue": 1, "name": "النجف" }],
  "shareUrl": "https://store.qalaa.iq/p/10"
}
```
`baseCommission` is what the seller earns; uplines earn `baseCommission × ratio^level` on top (section 11). The app hides it for guests and in «وضع العرض».

### `GET app/config`
```json
{ "minSupportedVersion": "1.0.0", "latestVersion": "1.0.0", "supportPhone": "07xxxxxxxxx", "supportWhatsapp": "9647xxxxxxxxx",
  "termsUrl": "https://store.qalaa.iq/terms", "privacyUrl": "https://store.qalaa.iq/privacy",
  "installmentsNotice": "الشراء بالأقساط اليومية متاح لأصحاب المحلات التجارية فقط",
  "referralEnabled": true, "createAccountsUnlockSales": 5,
  "referralRatioPercent": 50, "roundingUnit": 250, "minPayoutAmount": 10000,
  "apkUrl": "https://store.qalaa.iq/download" }
```
`apkUrl` is where update prompts send the user (setting `ApkDownloadUrl`; defaults to `{store}/download`). The app is distributed as an APK, not through the stores.

---

## 2. Auth (phone + OTP)

### `POST auth/otp/request`
```json
{ "phone": "07701234567", "purpose": "login" }
```
`purpose`: `login` (login or register). Response:
```json
{ "requestId": "b4f0…", "expiresInSeconds": 300, "resendAfterSeconds": 60, "isRegistered": false }
```
Errors: `invalid_phone`, `otp_rate_limited` (429, body includes `retryAfterSeconds`), `account_suspended` (403).

### `POST auth/otp/verify`
```json
{ "requestId": "b4f0…", "phone": "07701234567", "code": "123456",
  "registration": { "firstName": "علي", "fatherName": "حسين", "grandfatherName": "محمد", "surname": "الكعبي",
                    "cityValue": 1, "referralCode": "QA7K2M", "acceptTerms": true, "marketingOptIn": false },
  "device": { "platform": "android", "pushToken": "fcm-token-optional", "appVersion": "1.0.0" } }
```
- `registration` is required only when `isRegistered` was false (first time). `surname`, `referralCode`, `device` are optional; `acceptTerms` must be true.
- An invalid/unknown referral code → 400 `invalid_referral_code` (the user may clear it and retry).
Response:
```json
{ "accessToken": "…", "accessTokenExpiresAt": "…", "refreshToken": "…", "user": { /* Me */ } }
```
Errors: `otp_invalid` (400, includes `attemptsLeft`), `otp_expired`, `otp_too_many_attempts`.

### `POST auth/refresh`  `{ "refreshToken": "…" }` → same shape as verify (refresh token rotates).
### `POST auth/logout`  (auth) `{ "refreshToken": "…" }` → 204. Also removes the device push token if sent: `{ "refreshToken": "…", "pushToken": "…" }`.

---

## 3. Account (auth)

### `GET me` → `Me`
```json
{ "id": 42, "phone": "07701234567", "firstName": "علي", "fatherName": "حسين", "grandfatherName": "محمد", "surname": "الكعبي",
  "fullName": "علي حسين محمد الكعبي", "cityValue": 1, "cityName": "النجف",
  "referralCode": "QA7K2M", "referralLink": "https://store.qalaa.iq/r/QA7K2M",
  "referredBy": { "firstName": "حيدر" },
  "marketingOptIn": false, "completedSalesCount": 3, "canCreateAccounts": false, "createdAt": "…" }
```
### `PUT me` `{ "firstName","fatherName","grandfatherName","surname","cityValue" }` → `Me`. Phone cannot be changed.
### `PUT me/preferences` `{ "marketingOptIn": true }` → `Me`
### `POST me/devices` `{ "platform": "android|ios|web", "pushToken": "…", "appVersion": "1.0.0" }` → 204
### `DELETE me/devices/{pushToken}` → 204
### `DELETE me` `{ "confirm": "حذف" }` → 204
Deletes the account (App Store 5.1.1(v)). Personal data is anonymised. Referral links and the financial ledger stay for accounting.
Unpaid balance is forfeited and the UI must warn about that first.
### `GET me/product-commission/{productId}` → `{ "productId": 10, "baseCommission": 12000 }` (only when referral is enabled)

---

## 4. Installment accounts — removed

Users are independent from the company's customer records: there is no lookup of existing sales or installments by phone (`me/purchases*` were removed in v2).

## 5. Orders (auth)

### `POST orders`
Header `Idempotency-Key: <uuid generated once per submit attempt>`. Resending the same key returns the original order.
```json
{
  "cityValue": 1,
  "purchaseType": "DailyInstallments",
  "forSelf": true,
  "customer": {
    "firstName": "علي", "fatherName": "حسين", "grandfatherName": "محمد", "surname": "الكعبي",
    "phone": "07701234567", "secondaryPhone": null
  },
  "address": { "area": "حي الأنصار", "details": "شارع 20 قرب جامع …", "nearestLandmark": "مدرسة …",
               "latitude": 32.0, "longitude": 44.3 },
  "shop": { "name": "أسواق النور", "businessType": "مواد غذائية", "address": "…", "ownsShop": true },
  "items": [{ "productId": 10, "quantity": 1 }],
  "notes": "اتصلوا بعد الساعة 4"
}
```
Rules:
- `customer.firstName`, `fatherName` and `grandfatherName` are required, each one word (Arabic letters).
- `phone` is required. It defaults to the account phone when `forSelf=true`.
- `address.area` and `address.details` are required. Latitude and longitude are optional.
- `shop` is required when `purchaseType=DailyInstallments`, and `shop.name` and `shop.businessType` are required inside it. Daily installments are only for shop owners.
- `items` must have 1 to 10 lines, `quantity` 1 to 5. Each product must be visible and available in `cityValue`, and must allow the chosen `purchaseType`.
- Duplicate guard: if the same buyer phone already has an open order (New, UnderReview, Transferred or FieldInspection) for the same product, the API returns 409 `duplicate_open_order` with `orderNumber`.
Response 201 `OrderDetail` (below).

### `GET orders?status=&page=` → paged `OrderSummary`
```json
{ "id": 77, "orderNumber": "QO-2611-000077", "status": "UnderReview", "statusLabel": "قيد المعالجة",
  "purchaseType": "Cash", "forSelf": false, "customerName": "علي حسين محمد", "cityName": "النجف",
  "itemsSummary": "ثلاجة 18 قدم ×1", "estimatedTotal": 650000, "createdAt": "…", "updatedAt": "…" }
```
### `GET orders/{id}` → `OrderDetail`
`OrderSummary` +
```json
{ "customer": { … }, "address": { … }, "shop": { … } | null, "notes": "…",
  "items": [{ "productId": 10, "name": "ثلاجة 18 قدم", "thumbnailUrl": "…", "quantity": 1, "unitPrice": 650000, "dailyInstallment": null, "installmentDays": null }],
  "publicReason": "تم رفض الطلب لعدم توفر المنتج في منطقتك",
  "timeline": [{ "status": "New", "statusLabel": "طلب جديد", "at": "…", "note": null }],
  "canCancel": true }
```
`publicReason` is only set for `Denied`/`NotSold`. Internal reasons are never returned to customers.
### `POST orders/{id}/cancel` `{ "reason": "اختياري" }` → `OrderDetail`. Only allowed while `New` or `UnderReview`.

---

## 6. Earnings (auth) — أرباحي

### `GET earnings/summary`
```json
{ "directTotal": 24000, "networkTotal": 9000, "grandTotal": 33000,
  "pending": 12000, "available": 15000, "paid": 6000, "frozen": 0,
  "balance": 15000, "pendingPayoutRequest": null,
  "minPayoutAmount": 10000 }
```
`grandTotal` = direct + network (Reversed excluded, Clawback subtracted). `balance` = available − amount of open payout request.
### `GET earnings/entries?kind=&status=&page=` → paged
```json
{ "id": 501, "kind": "Network", "level": 2, "amount": 3000, "status": "Pending", "statusLabel": "معلقة",
  "orderNumber": "QO-2611-000077", "productSummary": "ثلاجة ×1", "sourceLabel": "من مبيعات مصطفى جاسم (المستوى 2)",
  "baseCommission": 12000, "createdAt": "…", "availableAt": "…", "note": null,
  "sellerName": "مصطفى جاسم", "orderTotal": 780000, "cityName": "النجف" }
```
`level=` filters to one level of the network (0 = own sales). Network entries name the member who sold (`sellerName`, network entries only) and show the order and its value. The buyer is never shown.
### `GET earnings/monthly?months=6` → `[{ "month": "2026-11", "direct": 12000, "network": 4500, "sales": 3 }]`, oldest first, empty months included (max 24).
### `POST earnings/payout-requests` `{ "amount": 15000, "method": "cash|zaincash|qicard", "accountDetails": "اختياري" }` → payout. One open request at a time.
### `GET earnings/payout-requests` → list `{ "id","amount","status","statusLabel","method","createdAt","processedAt","note" }`

## 7. Network (auth) — شبكتي

### `GET network/summary`
```json
{ "referralCode": "QA7K2M", "referralLink": "https://store.qalaa.iq/r/QA7K2M", "referredBy": { "firstName": "حيدر" },
  "directCount": 4, "networkSize": 17, "newThisMonth": 3,
  "networkEarnings": 9000, "networkEarningsThisMonth": 1500, "networkSalesCount": 6, "directEarnings": 24000,
  "completedSalesCount": 3, "salesToUnlockAccountCreation": 2, "canCreateAccounts": false,
  "levels": [ { "level": 1, "members": 4, "newThisMonth": 1, "sales": 3, "earnings": 6000 },
              { "level": 2, "members": 9, "newThisMonth": 2, "sales": 3, "earnings": 3000 } ] }
```
`levels` is the pyramid: level 1 = people you invited, level 2 = people they invited, and so on. `sales`/`earnings` are your cut from that level.
### `GET network/members?parentId=&page=&pageSize=` → paged `NetworkMember`
Children of `parentId` (default: you). `parentId` must be inside your downline, otherwise 404.
```json
{ "id": 1042, "name": "علي حسن", "phone": "07701234567", "cityName": "النجف", "level": 1, "joinedAt": "…",
  "directCount": 2, "networkSize": 5, "completedSales": 3, "branchSales": 7, "earnedForYou": 9500, "isDeleted": false }
```
`phone` is set for your own invitees (level 1) only. `branchSales` / `earnedForYou` cover the member and everyone below them.
### `GET network/direct?page=` → paged `{ "firstName": "حسين", "joinedAt": "…", "networkSize": 3 }` (kept for the web store).
### `POST network/accounts/otp` (requires `canCreateAccounts`)
`{ "phone": "07…", "firstName","fatherName","grandfatherName","surname","cityValue" }` → `{ "requestId","expiresInSeconds","resendAfterSeconds" }`.
The OTP SMS goes to the NEW person's phone. Its text says "<user first name> يطلب إنشاء حساب لك في تطبيق قلعة الضمان. رمز الموافقة: 123456". Sharing the code is the person's consent.
Error: `phone_already_registered` (409).
### `POST network/accounts/verify` `{ "requestId", "phone", "code" }` → `{ "created": true, "firstName": "…" }`. The new account is linked under the current user.

## 8. Notifications (auth)
### `GET notifications?page=` → paged `{ "id","type","title","body","data": { "orderId": 77 }, "readAt", "createdAt" }`, plus top-level `"unreadCount"`.
### `POST notifications/{id}/read` → 204 · `POST notifications/read-all` → 204
Push payload (FCM `data`) mirrors `{ "type", "notificationId", "orderId"?, "productId"? }` for deep linking.

---

## 9. Admin (dashboard) — `admin/*`, gateway JWT, roles المدير / مدير مبيعات

Every mutating admin call is written to the audit log (actor name, role, action, entity, before/after JSON, time).
Customer tokens on admin routes get 401; gateway tokens with any other role get 403. Dates are `YYYY-MM-DD` and `to` is inclusive.
Exact admin row shapes are the records in `backend/BE_CustomerApp/Domain/**` (e.g. `AdminOrderRow`, `AdminUserRow`, `AdminCommissionRow`, `AdminPayoutRow`).

### Orders screening (فلترة طلبات التطبيق)
- `GET admin/orders?status=&cityValue=&q=&from=&to=&page=` → paged `AdminOrderRow`:
  `{ id, orderNumber, status, statusLabel, purchaseType, cityValue, cityName, customerName, customerPhone, firstName, fatherName, grandfatherName, surname, itemsSummary, estimatedTotal, submittedBy: { userId, fullName, phone }, forSelf, createdAt, salesRequestId, otherOpenOrdersSamePhone }`
- `GET admin/orders/counts?cityValue=` → `{ "New": 3, "UnderReview": 1, …, "all": 12 }` (tab badges)
- `GET admin/orders/{id}` → `{ order: AdminOrderRow, customer, address, shop|null, notes, items, publicReason, internalNote, timeline:[{status,statusLabel,at,note,isInternal,actor}], salesRequest:{cityValue,salesRequestId,branchStatus,branchRejectReason,lastSyncAt}, reviewedBy, decidedBy, decidedAt }`.
  `review`, `approve`, `deny`, `sync` and `public-reason` all return this same object.
- `POST admin/orders/{id}/review` → marks `UnderReview` (auto when an admin opens a `New` order).
- `POST admin/orders/{id}/approve` `{ "note": "اختياري", "salesNotes": "ملاحظة تضاف للطلب في المنظومة" }`
  Sends the order to the company system: the gateway creates a sales request in the order's branch, and it appears on the existing «طلبات البيع» page.
  The order becomes `Transferred` and stores `salesRequestId`. The customer is notified.
  Returns the order. Error 502 `gateway_unavailable`, after which the order stays `UnderReview`.
- `POST admin/orders/{id}/deny` `{ "publicReason": "سبب يظهر للزبون (مطلوب)", "internalNote": "اختياري" }` → `Denied` + push/inbox notification containing the public reason.
- `POST admin/orders/{id}/public-reason` `{ "publicReason": "…" }` → edit the customer-facing reason of a `NotSold` order.
- `POST admin/orders/{id}/sync` → pulls the latest branch status now.
- Matching/score is NOT computed here. The FE page calls the gateway's existing endpoints directly with the same token:
  `POST {gw}sales-manager/sales-requests/evaluate` `{ items: [{ requestId: <order id>, sourceCityValue: <cityValue>, customerName: "<first father grandfather surname>", customerPhone, key }] }`
  and `POST {gw}sales-manager/sales-requests/evaluation-hits` `{ requestId, sourceCityValue, customerName, customerPhone, category, page, pageSize }`.

### Catalog
- `GET admin/categories` · `POST admin/categories` `{ name, iconUrl, sortOrder, isVisible }` · `PUT admin/categories/{id}` · `DELETE admin/categories/{id}` (only if it has no products)
- `GET admin/products?q=&categoryId=&visible=&page=` → paged rows incl. `baseCommission`, `isVisible`, `soldCount`
- `GET admin/products/{id}` → full product:
  `{ id, name, categoryId, description, extraInfo, cashPrice, installmentPrice, dailyInstallment, installmentDays, allowCash, allowInstallments, baseCommission, isVisible, isFeatured, badge, sortOrder, media:[{type,url,sortOrder}], specs:[{name,value}], branchCityValues:[1,2], createdAt, updatedAt }`
- `POST admin/products` / `PUT admin/products/{id}` (same body without ids/timestamps) · `POST admin/products/{id}/visibility` `{ isVisible }`
- `POST admin/media` (multipart `file`) → `{ "url": "https://.../media/<guid>.jpg", "type": "image|video" }` (images ≤ 10 MB jpg/png/webp, video ≤ 100 MB mp4)
- `GET/POST/PUT/DELETE admin/banners[/{id}]` `{ title, subtitle, imageUrl, productId, categoryId, externalUrl, sortOrder, isVisible, startsAt, endsAt }`

### Users & network
- `GET admin/users?q=&status=&page=` → `{ id, fullName, phone, cityName, referralCode, referredBy: {id, fullName}, directCount, networkSize, completedSalesCount, totalEarned, balance, isSuspended, earningsFrozen, createdAt }`
- `GET admin/users/{id}` → profile + `upline: [{id, fullName, level}]` (to root) + counts + earnings summary
- `GET admin/users/{id}/network?depth=1&page=` → direct children (lazy tree: each row has `directCount` to expand)
- `POST admin/users/{id}/suspend` `{ suspended: true, reason }` — blocks login and new orders
- `POST admin/users/{id}/freeze-earnings` `{ frozen: true, reason }` — new and existing unpaid entries become/stay `Frozen`, payouts blocked

### Commissions & payouts
- `GET admin/commissions?userId=&orderId=&status=&kind=&from=&to=&page=` → entries with user + order info
- `POST admin/commissions/orders/{orderId}/reverse` `{ reason }` — reverses all entries of that sale (unpaid → `Reversed`; paid → a `Clawback` entry is added)
- `POST admin/commissions/adjustments` `{ userId, amount (±), reason }`
- `GET admin/payouts?status=&page=` · `POST admin/payouts/{id}/approve` · `POST admin/payouts/{id}/mark-paid` `{ reference }` · `POST admin/payouts/{id}/reject` `{ reason }`

### Settings
- `GET admin/settings` / `PUT admin/settings`
  `{ referralRatioPercent: 50, roundingUnit: 250, holdDays: 7, createAccountsUnlockSales: 5, minPayoutAmount: 10000, referralEnabled: true, supportPhone, supportWhatsapp, installmentsNotice }`

### Notifications
- `POST admin/notifications/broadcast` `{ title, body, audience: "all|marketingOptIn|city", cityValue?, productId? }`.
  `Promotion` broadcasts go only to users who opted in to marketing, per App Store 4.5.4.
- `GET admin/notifications/broadcasts?page=`

### Reports & audit
- `GET admin/reports/overview?from=&to=` → `{ users: {total,new}, orders: {byStatus:{…}, total}, salesCount, salesValue, commissions: {pending, available, paid, reversed}, topProducts:[{productId,name,count}], topPromoters:[{userId,fullName,sales,earned}] }`
- `GET admin/reports/export?type=orders|commissions|users|payouts&from=&to=` → `text/csv` (UTF-8 BOM)
- `GET admin/audit?entity=&actor=&from=&to=&page=` → `{ id, actorName, actorRole, action, entity, entityId, before, after, createdAt }`

---

## 10. Order status mapping from the company system

| Branch `SalesRequests.Status` | App `orderStatus` |
|---|---|
| New, Assigned, Viewed, Returned, Pending, PreparedForSale | Transferred |
| InProgress, Inspected, ConvertedToSale, LegalFollowup | FieldInspection |
| Completed | **Sold** → commissions are created |
| Rejected / soft-deleted | NotSold (default public reason «لم يتم إتمام البيع بعد الكشف الميداني») |

## 11. Commission rule (owner spec, section 6)

For a sale with base commission `A` (Σ product.baseCommission × qty, snapshotted when the accrual is created) and ratio `r` (default 0.5):
- the seller (the account that submitted the order, even if buying for themself) gets `A`;
- ancestor `k` (k = 1 for the person who invited the seller) gets `floor(A·r^k / unit) · unit`, with `unit` = rounding unit (default 250 IQD);
- the walk stops when the chain ends or the amount reaches 0, so the chain length can be unlimited;
- the total is always ≤ `A/(1−r)` (= 2A at 50%), because every level is floored;
- idempotency: one accrual set per order (unique `(OrderId, UserId, Level)` index plus `CommissionRuns.OrderId` unique).
