# Online promoters app («المروّجون أونلاين») — v1.0.0 (2026-11-07)

This is a separate app from the field agents app («تطبيق المسوّقين»). It has its own API, its own database schema, its own dashboard section and its own APK. There is no location tracking, no work day («الدوام») and no shop photo.

| Part | Where |
|---|---|
| API | `backend/BE_PromotersApp` (.NET 10 + Dapper). Tests: `backend/BE_PromotersApp.Tests` |
| App | `mobile/promoters_app` (Flutter, Android). Package `iq.qalaa.promoters_app`, named «مروّجو قلعة الضمان» |
| Dashboard | `FE_Company` → group «المروّجون أونلاين» (`/promoters-app-*` pages, `composables/promotersAppApi.js`) |
| Live API | `http://promoters.alsaaeidy.com/api/v1/` (HTTP only). The dashboard reaches it at `/promoters-app/` on admincompany |

## Accounts and commission split
- **Dashboard accounts** are created with a username, password and phone. They are **full accounts**: the promoter keeps 100% of the commission and can create sub-accounts.
- **Sub-accounts** are created in the app («فريقي») with a username, password, phone and the **creator's cut %**. The sub-account only ever sees its own commission (after the cut). It is never shown who created it or that a cut exists. Example: a commission of 25,000 with a 60% cut shows 10,000 to the sub-account, and the creator gets 15,000 for each sale. The sub-account's share per unit is rounded to whole dinars, and the creator gets the rest.
- A sub-account cannot create accounts. When the dashboard makes it a **full account** («جعله حساباً كاملاً»), it keeps 100% and can create its own sub-accounts. This applies to new orders only.
- The creator (in the app) and the dashboard can both change the cut. Each order keeps the product, price, commission and split it was made with.

## Orders → sale → commission
- The app asks for: product (from the dashboard's list) + quantity, the customer's 3-part name, phone, governorate and address, plus optional notes.
- Orders go straight to the company system («طلبات البيع» → غير المسندة) through the existing gateway. The source reads «المروج: أونلاين - username» and the notes carry the price, so no company-system change was needed. If the company system is unreachable, the order waits and is retried every 2 minutes.
- The sync worker follows each order: «في قسم المبيعات» → «قيد الكشف» → **«تم البيع»** / «لم يتم البيع».
- **Only a sale credits the wallet**: the seller gets their share and the creator gets theirs. If the company later cancels the sale, both amounts are taken back with reversal entries.

## Wallet and withdrawals
- The balance is the sum of the ledger `promoters.WalletEntries`. Entry types: sale, team commission, reversals, withdrawal, returned withdrawal, and dashboard adjustment. The app lists all of them («سجل الحركات»).
- **Withdrawals**: the promoter enters an amount and either a **Qi Card** account number or a **Zain Cash** number. The amount leaves the balance immediately, and only one request can be open at a time. The minimum (default 5,000) is set in the dashboard settings.
- Each request has a **chat** between the promoter and the dashboard (polled every 5 s on both sides). The dashboard marks a request «تم التحويل», with an optional reference, or «مرفوض», with a reason. A rejection returns the amount to the balance.

## Marketing material
The dashboard uploads photos, videos and PDFs (up to 300 MB each). In the app, the promoter can:
- view them: photos zoom, videos play in the app, and PDFs open in the phone's viewer;
- download them: photos and videos go to the gallery, and PDFs use the "save as" screen;
- share them through the Android share sheet.

## Dashboard («المروّجون أونلاين», المدير + المراقب only)
- **نظرة عامة**: numbers, top promoters and top products.
- **المروّجون**: create, edit, set a new password, suspend, full-account toggle, cut, wallet ledger, manual adjustment, and the account's team.
- **المنتجات والعمولات**: name, price and commission; show or hide in the app.
- **طلبات المروّجين**: order list with status tabs and a «مزامنة» button.
- **طلبات السحب**: withdrawal requests with the chat and paid / reject.
- **المواد التسويقية**: upload, rename and delete material.
- **الإعدادات**: support numbers, minimum withdrawal, and a message to all promoters.

## Notifications (v1.0.1, 2026-11-08)
- Phone notifications go through Firebase project **qalaa-agents**: the same project as the agents app, with a second Android app `iq.qalaa.promoters_app`. Its `google-services.json` lists both apps and is kept in `handover-secrets/promoters_app/`.
- The server reuses the project's service-account key (`/etc/qalaa-push/firebase-qalaa-agents.json`). `promoters-app.env` has `Push__Fcm__ProjectId=qalaa-agents` and `Push__Fcm__ServiceAccountFile=…`. Migration `0002_Devices.sql` stores the phones' tokens.
- The app sends its token at sign-in and through `POST me/devices`, and when Firebase renews it. Logging out removes it, and tokens that Firebase rejects are deleted.
- What is pushed: every in-app notification, which covers order sold / not sold / sale cancelled, commission from the team, dashboard chat replies, withdrawal paid / rejected, balance adjustments, account changes and dashboard messages. Tapping one opens the order, the withdrawal chat, the wallet or the notifications list.
- Phones without Google Play services (newer Huawei models) only see the in-app bell, which refreshes every minute and when the app opens.

## Server (169.58.236.52, deployed 2026-11-07)
- Service `qalaa-promoters-app` → `/opt/qalaa-runtime/PromotersApp` on 127.0.0.1:5430. Media is stored in `/opt/qalaa-runtime/PromotersApp-media`.
- Secrets: `/etc/qalaa-production/promoters-app.env` (600). It was generated on the server: the connection string and gateway keys come from `customer-app.env`, and it has a new `Jwt__Key`.
- Database: `promoters` schema inside `DatabaseCompanyNajaf`, created at startup.
- nginx: new `sites-available/qalaa-promoters.conf` (promoters.alsaaeidy.com, listen 80), plus `location /promoters-app/` in `qalaa-company.conf` (admincompany).
- Rollback: `/opt/qalaa-runtime/_rollback_promoters_20261107212353/` holds the previous `qalaa-company.conf`. To roll back, stop and disable the service and remove the promoters nginx site.
- App updates: publish with **`deploy/publish-promoters-apk.sh [apk]`** (Git Bash, from the repo root, after `flutter build apk --release`). It reads the version from `mobile/promoters_app/pubspec.yaml`, uploads `qalaa-promoters-<version>.apk` to `/opt/qalaa-updates/promoters/` (download link `http://admincompany.alsaaeidy.com/mobile-updates/promoters/…`) and writes `release.json` last. That version becomes the latest and the minimum: every older app shows the blocking «يرجى تحديث التطبيق» screen with the download button. The script refuses a version older than the published one. Bump the version before every publish. From 1.0.5 the app also re-checks when it comes back to the foreground.

## APK
`demo/apk/qalaa-promoters-live-v1.0.0.apk` is signed with its own key: `qalatsystemfromzaid-main/handover-secrets/promoters_app/promoters-release.jks`, with `key.properties` next to it. Keep both files: every future update must be signed with the same key.

Build:

```bash
cd mobile/promoters_app && flutter build apk --release
```

Tests:

```bash
dotnet test backend/BE_PromotersApp.Tests
```

```bash
cd mobile/promoters_app && flutter test
```

## Dashboard deployed (2026-11-07 21:46 server time)
- Built from PowerShell into `deploy/publish/AdminFrontend` with the Mapbox key from `handover-secrets/FE_Company/.env` (checked present in the bundle), then swapped into `/opt/qalaa-runtime/AdminFrontend`. The `mobile-updates` folder was carried over.
- Backup of the previous build: `/opt/qalaa-runtime/AdminFrontend.prev-promoters-20261107214608`.
- This build also shipped the other session's then-undeployed role/permission edits (router, layouts, director/monitor pages, price-board), with the user's approval.

- 2026-11-07: v1.0.0 installed on the test phone (Honor MTN_NX1) at the user's request; it reaches the live API (a wrong login returns the server's error message).

- 1.0.2 (2026-11-08): the customer name error says what is wrong (fewer than three names, «عبد …» counts as one, or not Arabic). The app checks the same rule as the server, and diacritics, tatweel and invisible keyboard marks are ignored. APK `demo/apk/qalaa-promoters-live-v1.0.2.apk`, installed on the test phone.

- 1.0.3 (2026-11-08): a sub-account never sees who created it or that a cut is taken. `me` no longer returns the creator or the cut, and the app shows neither. The "cut changed" notification says only «تغيّرت عمولتك», and the full-account notification no longer mentions a cut. Products, orders and the wallet show only the account's own commission. APK `demo/apk/qalaa-promoters-live-v1.0.3.apk`, installed on the test phone.

## v1.0.4 (2026-11-09): Qi Card rule, transfer receipts, photos and documents in the withdrawal chat
- **Qi Card**: the number must be digits only, 10 to 13 of them. The server enforces this and the app's field accepts only digits, up to 13. Arabic-Indic digits are converted to 0-9.
- **Transfer receipt**: «تم التحويل» in the dashboard now requires a picture of the receipt (JPG, PNG or WEBP, up to 10 MB), sent as a multipart `receipt` plus an optional `note` to `POST admin/payouts/{id}/paid`. The promoter sees it in the withdrawal screen.
- **Chat attachments**: both sides can send photos (JPG, PNG, WEBP) and documents (PDF, Word, Excel), up to 15 MB each, through `POST …/messages/attachment` (multipart `file` + optional `body`). Images show in the chat; documents download and open with the phone's viewer.
- **Private files**: receipts and chat files are **not** public. They are stored in `Receipts:Root` (`/opt/qalaa-runtime/PromotersApp-receipts`, set in the systemd unit) and served only through authorised endpoints:
  - `GET admin/payouts/{id}/receipt` and `GET admin/payouts/{id}/messages/{messageId}/attachment` (dashboard);
  - `GET wallet/payouts/{id}/receipt` and `GET wallet/payouts/{id}/messages/{messageId}/attachment` (the owner only).
- Migrations `0003_PayoutReceipts.sql` and `0004_ChatAttachments.sql`. nginx `qalaa-promoters.conf` now allows 20 MB uploads.
- Deployed 2026-11-09. Backups: `PromotersApp.prev-receipts-20261109074900` (with the old systemd unit and nginx file) and `AdminFrontend.prev-receipts-20261109075327`.
- Back up `/opt/qalaa-runtime/PromotersApp-receipts` along with the media folder.
- APK `demo/apk/qalaa-promoters-live-v1.0.4.apk`, installed on the test phone.

- **1.0.5 published 2026-11-09 as the mandatory version** (`deploy/publish-promoters-apk.sh`). Checked on the test phone: 1.0.4 showed the blocking update screen, and «تحديث الآن» opened the download of `qalaa-promoters-1.0.5.apk` in Chrome.
