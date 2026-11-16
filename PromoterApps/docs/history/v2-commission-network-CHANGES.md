# v2 — APK app focused on commissions and the network (2026-11-03)

The owner wants the app to be a commission app that is shared as an APK, not a store app for everyone. The store-ready v1 is zipped in `backups/v1-store-ready-2026-11-03.zip` (all source, docs and demo; no build outputs or `node_modules`).

## 1. Store restrictions removed

| v1 (store rule) | v2 |
|---|---|
| Network members' names and sales hidden; network commissions showed only the level | Full tree: names of every member on every level, city, join date, their network size, their sales, and what you earned from each branch. Phone (call / WhatsApp) for the people you invited directly. |
| Commission shown only on the product page, via a separate call | `baseCommission` is part of every product card. A gold «+12,000» chip on product tiles, a per-level earnings card on the product page, a «اربح أكثر» strip and a «الأعلى عمولة» sort. |
| In-app account deletion (Apple 5.1.1(v)) | Screen removed from the app. The API endpoint stays for the admin and web. |
| Marketing opt-in checkbox, unchecked by default (Apple 4.5.4); promotions only reached opted-in users | Checkbox and toggle removed; new users are opted in. Broadcasts go to everyone unless the dashboard picks the `marketingOptIn` audience. |
| Update buttons opened the App Store / Google Play | They open `apkUrl` from `app/config` (setting `ApkDownloadUrl`, default `{store}/download`). The web store's badges are now one «تحميل تطبيق أندرويد (APK)» badge (`VITE_APK_URL`). |
| Careful earning wording | The wording now talks about earning directly: «اربح من كل عملية بيع», ranks, «اسحب أرباحك». |

Kept on purpose:
- Commissions are paid only on completed sales confirmed by the company system. Nothing is paid for sign-ups and there is no joining fee.
- Guest browsing, so a user can show products to anyone.
- The «create an account for someone» consent OTP. It goes to the new person's phone.

## 2. The pyramid in the app

Bottom tabs: **الرئيسية · محفظتي · شبكتي · المبيعات · حسابي**

- **محفظتي (wallet):** withdrawable balance, this month's earnings, total / pending / paid, «اسحب أرباحك» button, 6-month stacked chart (own sales vs network), «من أين تأتي أرباحك؟» split, ledger (each network row shows who sold, their level, the product, the order value and your cut), and payout requests.
- **شبكتي (network):**
  - rank badge (عضو جديد → برونزي 5 → فضي 20 → ذهبي 50 → بلاتيني 150 → ماسي 500 members) with progress to the next rank;
  - network size, «+N this month», who invited you, invite card (copy, share, WhatsApp);
  - the **pyramid**: «أنت» on top, then one wider bar per level with its members, sales and your earnings. Tap a level to list its sales;
  - the **tree**: level-1 members. Tap a member to open the people they invited, and so on down every level.
- **المبيعات (sales):** «طلباتي» (orders you submitted, with your completed-sales count and commissions) and «مبيعات شبكتي» (every sale in your network with your cut, filterable by level).
- **الرئيسية:** an earnings card (balance, total, network size) instead of the old installments card.
- **وضع العرض (presentation mode):** the eye icon on the home bar, or the switch in «حسابي». It hides every commission figure on the shop screens, so the phone can be handed to a buyer. The app still looks like a normal shopping app to them.

## 3. Users are independent from the company's customers

The «مشترياتي وأقساطي» feature has been removed everywhere: app screens, web pages, `GET me/purchases*`, the purchases service, the gateway calls `accounts-by-phone` / `accounts/{city}/{customer}`, and the demo data. The company-repo endpoints from v1 (see CHANGES_IN_COMPANY_REPO.md) are no longer called. They can be left in place or removed there.

## 4. API changes (see API_CONTRACT.md)

- `catalog/products*`: added `baseCommission`, and `sort=commissionDesc`.
- `app/config`: added `apkUrl`.
- `earnings/entries`: added `level=` filter and `sellerName`, `orderTotal`, `cityName`. The order number is now shown on network entries too.
- `earnings/monthly?months=6`: new.
- `network/summary`: added `referredBy`, `newThisMonth`, `networkEarningsThisMonth`, `networkSalesCount`, `directEarnings`, `levels[]`.
- `network/members?parentId=`: new. Only members inside the caller's downline can be opened.
- Removed: `me/purchases`, `me/purchases/{cityValue}/{customerId}`.
- Network commission notifications now name the seller and the order.

## 5. Building the APK

```bash
flutter build apk --release --dart-define=API_BASE_URL=https://api.qalaa-store.iq/api/v1/
```
The output is `build/app/outputs/flutter-apk/app-release.apk`. It is signed with the release key when `android/key.properties` exists (see `mobile/customer_app/README.md`); without that file it falls back to the debug key. Keep using the same key: Android only installs an update over an existing APK when both are signed with the same key. Put the file at the `ApkDownloadUrl` and raise `LatestVersion` / `MinSupportedVersion` in the settings so installed apps prompt to update.
