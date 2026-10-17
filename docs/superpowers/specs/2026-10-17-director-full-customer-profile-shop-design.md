# Design: Full customer profile from director sales-auth + shop data 3-part section

**Date:** 2026-10-17  
**Status:** Approved (conversation) — pending user review of this file  
**Surfaces:** `director-follower-work.vue`, `sales-manager-customer-profile.vue`  
**Backend:** Prefer existing `SalesCustomerProfileDTO.FollowerShopEval` / `DelegateShopEval` (no BE change unless director profile path omits them)

## Problem

On the director follower work page, the inline sales-auth profile is a summary. Directors need a one-click path to the **full** customer profile (sales-manager customer profile), then a reliable **Back** that restores the same follower page and the same open customer.

Separately, **بيانات المحل** on that full profile currently shows only the sales-employee shop form (name, type, stock, revenue, dimensions, location, image). Follower and delegate evaluations already exist on the API but are not shown inside that section.

## Goals

1. Rectangular control next to **مصادق / بانتظار** labeled **عرض بروفايل الزبون الكامل**.
2. Navigate to `/sales-manager-customer-profile` for that customer (same city context).
3. Explicit **رجوع** returns to director follower work with the **same city, follower, tab, and open customer** profile panel.
4. Inside **بيانات المحل**, show three ordered subsections:
   1. بيانات موظف المبيعات (existing shop + location + image)
   2. تقييم المتابع (rating, note, stock, daily revenue, time, evaluator name)
   3. تقييم المندوب (rating, note, stock, daily revenue, time, evaluator name)
5. Missing subsection data shows an empty state inside that subsection; do not hide the other two.

## Non-goals

- Restructuring the inline director summary boxes beyond adding the button (optional later).
- Changing `/customer-profile` (legacy).
- Editing/creating shop evaluations from this UI.
- Deduplicating sales-auth cards (already shipped separately).

## Approach (chosen)

**FE-first + return query params** (not sessionStorage-only).

- Outbound navigation carries return context in the URL.
- Profile page shows a visible Back button when `returnTo` is present.
- Shop section UI binds to existing profile payload fields.

## Navigation contract

### Outbound (from `director-follower-work`)

Navigate to:

```
/sales-manager-customer-profile
  ?customerId=<id>
  &city=<cityValue>
  &returnTo=director-follower-work
  &followerId=<id>
  &tab=<currentTab>
  &resumeCustomerId=<id>
```

Optional: keep other existing query keys the director page already uses if required for city fan-out.

### Inbound restore (on `director-follower-work`)

When landing with `resumeCustomerId` (and matching `followerId` / `city` / `tab`):

1. Select the follower and tab from query (existing patterns).
2. After sales-auth (or relevant) rows load, set `profileRow` to the row matching `resumeCustomerId`.
3. Clear `resumeCustomerId` from the URL after apply (replace) so refresh does not fight the user, **or** keep it until the user closes the panel — prefer clear-after-apply with replace to avoid loops.

### Back button (on `sales-manager-customer-profile`)

- Visible when `returnTo === 'director-follower-work'` (and ideally `followerId` present).
- Label: **رجوع** (or **رجوع لعمل المتابع**).
- Builds the director-follower-work route with `city`, `followerId`, `tab`, `resumeCustomerId`.
- Does not replace browser history stack in a way that breaks a second Back; prefer `router.push` to the restore URL.

Browser chrome Back should also work if history includes the director page; the explicit button is required for users who opened the profile in a way that loses history (or deep-linked).

## UI: button on director page

- Placement: profile header next to the **مصادق / بانتظار** badge (not on every name card).
- Style: rectangular container / outlined or tonal `VBtn` (not a pill badge).
- Text: **عرض بروفايل الزبون الكامل**.
- Enabled when `customerId` is resolvable from `profileRow`.
- Click does **not** close the inline profile before navigate (state is restored via query).

## UI: بيانات المحل three subsections

Under existing heading **بيانات المحل** (per sale card where shop is shown today):

### 1) بيانات موظف المبيعات

Keep current fields from `saleShop(sale)`:

- اسم المحل، طبيعة العمل، قيمة الموجودات، الإيراد اليومي، الطول، العرض، المساحة
- `ShopLocationLink`
- shop image / empty message

If no shop: keep current «لا توجد بيانات محل.» for this subsection only (or show subsection title + empty).

### 2) تقييم المتابع

Bind `profile.followerShopEval` / `FollowerShopEval`:

- التقييم (`Rating`)
- الملاحظة (`Note`)
- قيمة بضاعة المحل (`ShopStockEstimatedValue`)
- قيمة الوارد اليومي (`EstimatedDailyRevenue`)
- وقت المصادقة (`EvaluatedAtUtc`)
- اسم المقيّم (`EvaluatorName`) when present

Empty: «لا يوجد تقييم متابع.»

### 3) تقييم المندوب

Bind `profile.delegateShopEval` / `DelegateShopEval` (same field set as follower).

Empty: «لا يوجد تقييم مندوب.»

Visual: three clear subheadings inside one **بيانات المحل** block (dividers or nested `h6`), not three top-level page cards competing with المبيعات / المستندات.

## Data / API

- Confirmed: `SalesShopProfileService.GetCustomerProfileAsync` already loads `FollowerShopEval` and `DelegateShopEval` via `LoadShopFieldEvaluationsAsync`.
- FE currently does **not** reference these properties — display-only change expected.
- Director path uses `directorCustomerProfilePath` / `directorGet` — verify the director customer-profile response includes the same two properties; if missing, extend the director aggregator to pass through (small BE/GW change). Do not invent a second eval loader on the FE.

## Permissions

- Same access rules as today for opening sales-manager customer profile from director/monitor flows.
- Read-only director behavior on that page remains unchanged.

## Testing

1. Open sales-auth customer → badge visible → click full-profile button → SM profile loads correct customer/city.
2. Click رجوع → director follower work restores city, follower, tab, and opens the same customer panel.
3. Shop section shows employee block with image/location when present.
4. After follower auth exists, subsection 2 shows rating/note/values.
5. After delegate eval (or SalesRequest shop rating fallback), subsection 3 shows data.
6. Customer with only employee shop still shows empty states for 2 and 3.
7. Regression: opening SM profile from sales-manager lists (no `returnTo`) does not show a broken Back target.

## Impact

| Area | Impact |
|------|--------|
| `FE_Company` director-follower-work | Button + outbound query + resume on mount |
| `FE_Company` sales-manager-customer-profile | Back button + بيانات المحل 3-part layout |
| `BE_Company` / GW | Only if director profile DTO omits shop evals |
| Flutter / other apps | None |
| DB | None |

## Open points resolved in conversation

- Target page: **sales-manager-customer-profile** (not legacy customer-profile).
- Return: same page + same open customer.
- Shop section location: full profile page, expand existing بيانات المحل.
