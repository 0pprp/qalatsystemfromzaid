# تطبيق المروج الميداني — features and change log

> Formerly called «تطبيق الزبون» (v1) and «تطبيق المسوّقين» (v3+). The sections below are in date order, starting with v3 (2026-11-04).

## v3: field-agents app (2026-11-04)

The app is now for the company's field advertisers (agents) only. There are no customer accounts, no invitations and no pyramid. An agent shows products, submits a **sale order** for a customer, and follows it until it becomes a **sale**, then until the sale is **fully paid**. Agents record their working day («الدوام») with a live location trail that the dashboard monitors.

Backups of the earlier versions:
- v1 (customer store): `../../docs/history/v1-customer-store-*.md`
- v2 (commission network): `../../docs/history/v2-commission-network-CHANGES.md` (full source zips stay outside the repo, in `Customer_website_app/backups/`)

## Accounts: username + password from the dashboard
- Dashboard → «تطبيق المسوّقين» → **المسوّقون** (`/customer-app-agents`): create, edit, set a new password, and suspend. Creating takes a username, password, 3-part name (+ optional surname) and phone. Agents belong to the company HQ, not to a branch (2026-11-05): the server stores no branch for them and ignores one if sent, and each sale order still picks its own branch. The agents, attendance and shop-pins pages therefore have no branch filter. A new password signs the agent out of every device. Suspending blocks sign-in.
- App: a username/password login screen; every screen requires sign-in (no guest browsing).
- Removed: SMS/OTP sign-in, self-registration, referral codes, «create an account for someone», the network screens, and in-app profile editing (the dashboard manages names).
- API: `POST auth/login { username, password, device }`. Usernames are case-insensitive. Passwords are PBKDF2-SHA256 hashed. `POST auth/otp/*` and `network/*` no longer exist.

## Sale orders: shop photo + location are mandatory
- New wizard step «المحل والموقع»: a camera photo of the shop and a GPS pin on a map (the agent can move the pin). The order cannot be sent without both.
- Shop name and activity are optional for cash orders and required for daily installments.
- API: `POST orders/shop-photo` (multipart `file`, JPEG/PNG/WebP up to 8 MB) returns `{ url }`. `POST orders` then requires `shopPhotoUrl` (an uploaded photo) and `address.latitude/longitude`.
- The photo link and a Google Maps link are included in the notes sent to the company system. The dashboard order dialog shows the photo.

## From order to sale to fully paid
- New status **Finished** («مكتمل التسديد»), after Sold («تم البيع - قيد التسديد»).
- When the company system converts the request into a sale, the API stores the sale id (`SalesRequests.ConvertedToSaleId`). The sync worker then polls the sale's payments. It stores total, paid, remaining, payment count and last payment date, and moves the order to Finished when nothing remains, with a notification.
- App «الطلبات والمبيعات» has 4 tabs: قيد المتابعة · قيد التسديد (progress bar) · مكتملة · مرفوضة/ملغاة. The order page shows the payment panel, the shop photo and the map.
- API: `GET orders?status=Sold,Finished` (comma list), `GET orders/counts`, and `sale` on every order. The dashboard «مزامنة» button also refreshes payments.
- **Company system (new endpoints, see CHANGES_IN_COMPANY_REPO.md):** branch `GET api/internal/customer-app/sales/{saleId}/progress` and gateway `GET api/internal/customer-app/sales/{cityValue}/{saleId}/progress`. The branch reads the account posted from the sales draft (`SalesDrafts.CustomerId` → `View_CustomersDelegate`).

## Working day («الدوام») and live location
- **First time the app is opened each day:** a sheet asks «هل تبدأ الدوام؟» with the options «بدء الدوام» or «لاحقاً».
- **Tab «الدوام»:**
  - a live map (OpenStreetMap) with today's route, the start flag and the current position;
  - status: working since, total hours today, start time, point count, and send status;
  - one button to start/resume or finish the day, plus the list of today's shifts;
  - several shifts per day are allowed; the day total is their sum.
- **Tracking while the day is started:**
  - the phone's location is sent while the day is started: a point every ~15 m of movement or 20 s;
  - points are sent in batches every 45 s and buffered on the phone while offline;
  - it runs as an Android foreground service with a persistent «الدوام مستمر» notification, so it keeps working while the app is in the background;
  - it stops if the agent force-closes the app or Android kills it. Points buffered on the phone are sent when the app opens again; nothing is recorded while it is closed.
- **Auto-close:** a day left open past midnight (Iraq time) is closed automatically at its last point.
- **Dashboard → «دوام المسوّقين»** (`/customer-app-attendance`):
  - per day, every agent with start time, finish time, total hours, number of shifts and last position;
  - a live map of everyone (refreshes every 30 s for today);
  - click an agent to see their full route and shifts for that day;
  - «سجل الأيام» shows the last 30 days with daily hours and a link to each day's route.
- **Agent API:**
  - `GET work/today`
  - `POST work/start`
  - `POST work/end`
  - `POST work/locations { points: [{ latitude, longitude, accuracy, speed, battery, recordedAt }] }`, which answers `{ accepted, isWorking }`. When `isWorking` is false the app stops tracking.
- **Dashboard API:**
  - `GET admin/attendance?date=&cityValue=`
  - `GET admin/attendance/live`
  - `GET admin/attendance/{userId}/day?date=`
  - `GET admin/attendance/{userId}/history?from=&to=`

## Commissions: hidden
- The single-level commission ledger is still recorded on every sale (uplines no longer exist).
- The app shows no commission or wallet while the new setting `CommissionsVisible` is false (the default).
- Turning it on (`PUT admin/settings`) brings back the wallet («محفظتي» in «حسابي») and the commission figure on product pages.

## Not changed / to decide
- **Web store** (v1 `web/store`): removed from the repo on 2026-11-16. It used SMS sign-in, which no longer exists. The source is still in `Customer_website_app/web/store` outside the repo.
- **Old customer accounts** from v1/v2 remain in the database but cannot sign in (they have no username). The old «مستخدمو التطبيق» dashboard page was removed from the menu but still works at `/customer-app-users`.
- **iOS** was not set up for background location; the app is distributed as an Android APK.

## Demo APK
`demo/apk/qalaa-agents-demo-v3.apk` runs entirely on built-in sample data: no server and no SMS. Sign in with `agent1` / `123456` (or `agent2`). Orders at every stage are included, and a sample morning route around the phone's position is added the first time «الدوام» opens. Real GPS and camera are used.

## v3.1 (2026-11-05)

**Prospect pins («المحلات المحتملة»)**
- On the «الدوام» map, tap «تثبيت محل» or long-press the map. Required: shop name and how interested the owner seemed, from «غير مهتم إطلاقاً» to «مهتم جداً». Optional: type of shop, phone, estimated goods value, and length × width in metres.
- Every agent sees every pin in the area shown, coloured by interest. Their own pins have a dark outline and can be edited or deleted. Each pin has call and directions buttons.
- Dashboard → «المحلات المحتملة»: map and list with filters; the dashboard can delete pins.
- API (agent):
  - `GET prospects?minLat&minLng&maxLat&maxLng`
  - `POST prospects`
  - `PUT /DELETE prospects/{id}`, own pins only
- API (dashboard): `GET admin/prospects` (paged, filters) and `DELETE admin/prospects/{id}`.

**Stationary alert**
- While the day is started, if an agent stays within 150 m of one place for 60 minutes, the dashboard is alerted. The worker checks every 2 minutes; both thresholds are configurable with `WorkAlerts:StationaryMinutes` and `WorkAlerts:RadiusMeters`.
- The alert closes when the agent moves away or finishes the day.
- The app now records a point at least every 2 minutes even when standing still, so a phone that stopped sending is not mistaken for an agent standing still.
- Dashboard (المدير / المراقب): a bell in the top bar with a count, toast, sound and browser notification. «دوام المسوّقين» shows the alerts on the map and in the list.
- API:
  - `GET admin/attendance/alerts?date&open`
  - `GET admin/attendance/alerts/unseen`
  - `POST admin/attendance/alerts/{id}/seen`
  - `POST admin/attendance/alerts/seen-all`

**Roles**
- The «تطبيق المسوّقين» dashboard section and `admin/*` API are for المدير and المراقب only. مدير مبيعات is no longer allowed.

**Orders go straight to «طلبات البيع»**
- A new order is sent to the company system the moment it is created, landing in «غير المسندة» with source «المروج: الاسم». There is no review or filtering step.
- If the branch is unreachable, the order is kept and retried every 2 minutes.
- The status «محوّل» now reads «في قسم المبيعات», and the app's timeline no longer has the «قيد المعالجة» step.

**Demo APK:** `demo/apk/qalaa-agents-demo-v3.1.apk` (version 1.2.0+4). It includes sample pins, some made by a colleague (زينب).

## Live deployment (2026-11-05, VPS 169.58.236.52, HTTP only)
- **Agents app API:** `http://agents.alsaaeidy.com/api/v1/` → nginx `qalaa-agents.conf` → systemd `qalaa-customer-app` → `/opt/qalaa-runtime/CustomerApp` on 127.0.0.1:5420. Media is stored in `/opt/qalaa-runtime/CustomerApp-media`.
- **Secrets:** `/etc/qalaa-production/customer-app.env` (root, 600). It was generated on the server by a script that copied the gateway keys and the branch SQL login without printing them.
- **Database:** the app shares the company database `DatabaseCompanyNajaf` on the same SQL Server, keeping all of its own tables in a dedicated `agents` schema (migrator runs `CREATE SCHEMA agents` at startup). It has no database of its own: the `haider` login cannot create databases, no `sa` password was available, and the server had no spare RAM for a second SQL instance (decided 2026-11-05). The `agents` schema never touches the company `dbo` tables, and the server's existing company-DB backups cover it. Connection string `Database=DatabaseCompanyNajaf` is in `/etc/qalaa-production/customer-app.env`. To relocate it later to its own `CustomerAppDb`, an SQL admin creates that database and grants `haider`, then the connection string is repointed — the service recreates its tables on first start.
- **Dashboard:** `admincompany` serves `/customer-app/` → 127.0.0.1:5420. The new dashboard build is in `/opt/qalaa-runtime/AdminFrontend`; the previous one is kept as `AdminFrontend.prev-agents-v31`.
- **Company services:** the gateway and all 20 `qalaa-company-*` branch services were redeployed from the local repo. This also shipped the other pending local changes (sales overview, 2026-11-05).
- **Rollback archive:** `/opt/qalaa-runtime/_rollback_agents_v31_20261105_050004/` (services, frontend, nginx config).
- **Not changed:** HTTP/HTTPS setup and the `mobile-updates` channels; no phone was updated.
- **Live APK:** `demo/apk/qalaa-agents-live-v3.1.apk`.

## v4 (2026-11-06): a field-agents tool, not a store

**App (1.3.0+5)**
- The store is gone: no catalog, product pages, banners, cart, wallet/commissions or deep links.
- «الرئيسية» is an overview: the work day (start button / time worked), «طلب بيع جديد» and «تثبيت محل» buttons, today's and this month's numbers (orders, sales, open, waiting payment, fully paid, pins) and the 5 latest orders.
- «طلب بيع جديد» (`/orders/new`) has 5 short steps: what was sold (typed name + quantity, up to 10 lines; cash or daily installments) → customer → address (branch, area, details) → shop photo + location (shop name/type required for installments) → review. No prices anywhere.
- The order details page shows the items, status steps, customer (tap to call), shop photo and map; sold orders show payment progress once the company system reports it.

**Server**
- Migration `0004_FreeTextItems.sql`: order items are `Name` + `Quantity`; the catalog, commission and payout tables and their settings were dropped.
- `GET me/overview` for the home page; `admin/reports/overview` now returns totals, orders per status, per-agent performance (orders, sales, pins, days/hours worked) and the most sold items. CSV exports: orders, agents, prospects.
- Notes sent with each company sales request are plain and short: purchase type, shop, second phone and the agent's notes (no links, prices or order numbers). The item goes in the request's product line and the agent is its source («المروج: الاسم»).
- Settings: support phone/WhatsApp, latest/minimum app version and APK link only. Dashboard messages go to all active agents.

**Dashboard («تطبيق المسوّقين»)**: نظرة عامة · طلبات المروّجين · المسوّقون · دوام المسوّقين · المحلات المحتملة · إعدادات التطبيق. Removed: منتجات التطبيق, الأقسام, البنرات والعروض, العمولات والصرف, مستخدمو التطبيق, تقارير التطبيق (replaced by نظرة عامة).

**Deployed 2026-11-06** (server + dashboard). Backups: `/opt/qalaa-runtime/CustomerApp.prev-v4-20261106172504`, `/opt/qalaa-runtime/AdminFrontend.prev-v4-20261106172504`. APKs: `demo/apk/qalaa-agents-demo-v4.apk`, `demo/apk/qalaa-agents-live-v4.apk` (not installed on any phone).

## v4.1 (2026-11-06)

- **Usernames** may be Arabic or English (2–30 letters/digits and . _ -, no spaces). Migration `0005_ArabicUsernames.sql` makes `Users.Username` NVARCHAR. Sign-in ignores English case, Arabic diacritics and tatweel, and treats Arabic-Indic digits as 0–9.
- **Passwords**: any non-empty password (max 256 characters).
- **Time zone fix**: the server stores UTC but sent dates without a zone, so the app and dashboard read them as local time (3 hours early, and work-session durations 3 hours too long). All dates are now sent with "Z" (`Infrastructure/UtcDateTimeConverter.cs`); the app also reads zone-less dates as UTC and always displays Iraq time (UTC+3). Dashboard date filters (orders, action log) now use Iraq calendar days.
- No demo APK any more (the app's demo mode is kept only for automated tests). Live APK: `demo/apk/qalaa-agents-live-v4.1.apk` (1.3.1+6), not installed on any phone.
- Deployed 2026-11-06; backups `CustomerApp.prev-v41-20261106173458`, `AdminFrontend.prev-v41-20261106173458`.

## v4.2 (2026-11-06)

**Short sale order (app 1.4.0+7)**
- The app opens on «طلب بيع» (first tab). One page: product from a list (ايفون، دراجة، كرسي مساج، شاشة، اخرى → typed name), customer's full name in one field (three names at least), phone, branch, address. Shop photo and GPS location are optional (collapsed section). Errors show under the fields.
- Sales are daily installments only: no choice in the app; the server defaults to `DailyInstallments`. Notes sent to the company no longer repeat the purchase type (only for cash orders from older apps).
- Server: `customer.fullName` is split into the stored name parts («عبد/أبو» joined to the next word; anything after the third name is the surname). Area, shop name/type, shop photo and location are optional; one address line is enough.
- Tabs: طلب بيع · ملخصي (the overview) · الطلبات · الدوام · حسابي.

**Location while the app is closed**
- During «الدوام» on Android, tracking runs in `lib/features/work/data/background_tracker.dart` (flutter_background_service): a foreground service with its own isolate and a «الدوام مستمر» notification. It keeps recording when the app is minimised, swiped away from recent apps or closed by the system. Points are buffered on the phone and sent every minute.
- The service does not use the sign-in tokens (refresh tokens are single-use). At the start of the day the app gets a per-session key (`POST work/tracking-key`) and the service sends points to `POST work/track` with header `X-Tracking-Key`. The key stops working when the day ends, and the service then stops itself. Migration `0006_TrackingKeys.sql` stores only the key's SHA-256.
- Not automatic after the phone restarts (Android blocks starting location tracking from boot without «Allow all the time»): it resumes when the agent opens the app. Phones with aggressive battery saving may still stop it; allow the app to run in the background in the phone's battery settings.
- Deployed 2026-11-06 (backup `CustomerApp.prev-v42-20261106180701`). APK: `demo/apk/qalaa-agents-live-v4.2.apk`, not installed on any phone.

## v4.2.1 (2026-11-07, app 1.4.1+8, app only)

- Tested on the test phone (Honor MTN_NX1, Android 16): with «الدوام» started and the app swiped away, points kept reaching the server every 2 minutes (standing still); ending the day stopped the service and its notification.
- Fixed the grey «الدوام» map. flutter_map 8.3 sends a late "size changed" event with the first camera just after the map is laid out; a camera move made before it (the old route fit right after the first frame) lost its tiles until the map was dragged. The route known at the first build is now the map's starting camera (`initialCameraFit`), and programmatic moves (route arriving later, follow, recenter) wait until the map has settled.
- Shop location card: no camera move before its map exists; hint no longer says «إلزامي».
- APK: `demo/apk/qalaa-agents-live-v4.2.1.apk` (installed only on the test phone).

## v4.2.2 (2026-11-07, app 1.4.2+9, app only)

- «تثبيت محل» on «ملخصي» opens the pin form straight away (`pinShopHere` in `prospect_sheets.dart`) at the agent's position: the live work-day position, or a fresh GPS fix when not on the clock. After saving, the «ملخصي» numbers and the «الدوام» map pins reload (`prospectsVersionProvider`). The button label stays on one line.
- APK: `demo/apk/qalaa-agents-live-v4.2.2.apk` (installed only on the test phone).

## v4.3 (2026-11-07, app 1.4.3+10)

- «ملخصي» has a «محلاتي المثبّتة» card: how many shops the agent pinned (all time, and this month) and how many are at each interest level (مهتم جداً … غير مهتم إطلاقاً) with bars. Server: `me/overview` adds `pinsTotal` and `pinsByInterest` (all five levels, most interested first; deleted pins not counted). Older apps ignore the new fields. Editing or deleting a pin on the map refreshes it.
- App icon: the company logo's emblem (castle, circle and plane, without the wording) on white; Android adaptive icon plus the legacy sizes (`res/mipmap-*`, `mipmap-anydpi-v26/ic_launcher.xml`). Generated from `assets/images/logo.png`.
- Server deployed 2026-11-07 (backup `CustomerApp.prev-v422-20261107014505`). APK: `demo/apk/qalaa-agents-live-v4.3.apk` (installed only on the test phone).

## v4.4 (2026-11-07, app 1.4.4+11)

- «محلاتي المثبّتة» (`/pins`, `my_pins_screen.dart`): every shop the agent pinned, newest first, with a search (name, type, phone) and a filter per interest level (counts from «ملخصي»). Opened from the «ملخصي» pins card (an interest row opens the list filtered to that level) and from «محلاتي» on the «الدوام» map.
- Tapping a shop (`/pins/:id`) shows its location on a map and everything entered (type, goods value, size, phone, added/edited dates), with call, directions (Google Maps), and edit / delete.
- Server: `GET prospects/mine?interest=&q=&page=` (own pins only, paged).
- Server deployed 2026-11-07 (backup `CustomerApp.prev-v43-20261107020650`). APK: `demo/apk/qalaa-agents-live-v4.4.apk` (installed only on the test phone).

## v4.5 (2026-11-07, app 1.4.5+12, app only)

- «الدوام» starts by itself when the agent sends a sale request or pins a shop and the day is not running (`WorkController.ensureWorking`, `work/presentation/auto_start.dart`). The agent sees «بدأ الدوام تلقائياً…», or why it could not start (GPS off, location permission refused, offline). The order / pin is never held back: for orders the start runs alongside sending; for pins it runs after the pin is saved.
- An ended day is resumed the same way (a new session, like «استئناف الدوام»).
- APK: `demo/apk/qalaa-agents-live-v4.5.apk` (installed only on the test phone).

## v4.6 (2026-11-07, app 1.4.6+13): phone notifications (Firebase)

- Firebase project **qalaa-agents** (company Google account). App: `android/app/google-services.json` + the `com.google.gms.google-services` Gradle plugin (settings.gradle.kts and app/build.gradle.kts). Files kept in `qalatsystemfromzaid-main/handover-secrets/` (`google-services.json`, `qalaa-agents-firebase-adminsdk-*.json`).
- Server: the service-account key is at `/etc/qalaa-push/firebase-qalaa-agents.json` (root:qalaa, dir 750, file 640; `/etc/qalaa-production` is root-only, so the app could not read it there). `/etc/qalaa-production/customer-app.env` has `Push__Fcm__ProjectId=qalaa-agents` and `Push__Fcm__ServiceAccountFile=/etc/qalaa-push/firebase-qalaa-agents.json` (backup `customer-app.env.bak-push-*`). With these set, `FcmPushSender` replaces `NullPushSender`.
- What reaches phones (also with the app closed): dashboard messages («إعدادات التطبيق» → messages to agents) and order status changes. A tap opens the order (or the notifications page).
- Status-bar icon: `res/drawable-*/ic_stat_notify.png` (emblem silhouette), tint #006A64 (manifest `default_notification_icon` / `default_notification_color`). Channel text no longer mentions earnings/offers.
- Agents signed in before this version are asked for the notification permission at app start (Android shows it only while undecided).
- Phones without Google Play services (e.g. newer Huawei) cannot receive Firebase notifications; they still see them inside the app.
- APK: `demo/apk/qalaa-agents-live-v4.6.apk` (installed only on the test phone).

## Releasing the app (2026-11-07): the published APK is the mandatory version

- App versions are no longer dashboard settings (removed from «إعدادات التطبيق»; migration `0007_VersionFromRelease.sql` deletes the old keys).
- Publish with **`deploy/publish-apk.sh [apk]`** (Git Bash, after building the release APK). It reads the version from `pubspec.yaml`, uploads `qalaa-agents-<version>.apk` to `/opt/qalaa-updates/agents/` (served at `http://admincompany.alsaaeidy.com/mobile-updates/agents/`), repoints `qalaa-agents-latest.apk`, and writes `release.json` last (`{"version","apkUrl"}`). It refuses a version older than the one published.
- The server (`AppReleaseService`, setting `Updates__ReleaseFile=/opt/qalaa-updates/agents/release.json` in `/etc/qalaa-production/customer-app.env`) reports that version as both `latestVersion` and `minSupportedVersion` in `app/config`: every older app shows the blocking «يرجى تحديث التطبيق» screen with the download link. The file is re-read when it changes (no restart). No / broken file → `0.0.0` (nothing forced).
- Always bump the version in `pubspec.yaml` before publishing; publishing the same version again does not reach phones that already have it.
- **1.4.6 published 2026-11-07** (mandatory). Backups: server `CustomerApp.prev-v46-20261107030220`, dashboard `AdminFrontend.prev-noversions-20261107030346`.

## 2026-11-07: the company's rejection reason reaches the agent (server only)

- When the company system marks a sale request not sold, its reason (the sales employee's `RejectionReason`, or the delete reason) is now the agent's reason: in the order («سبب عدم إتمام البيع»), on the «لم يتم البيع» timeline step and in the notification. The generic «لم يتم إتمام البيع بعد الكشف الميداني…» is only used when the company gave no reason. Before, the reason was kept only as an internal note. A sale cancelled after being sold shows «تم إلغاء عملية البيع: <reason>».
- Migration `0008_CompanyRejectReasonForAgents.sql` fixed requests rejected earlier (their stored company reason replaces the generic text; the timeline step gets the note). No app update needed.
- Test hook: `FakeGatewayClient.Reject(orderUuid, reason)`. Deployed 2026-11-07 (backup `CustomerApp.prev-reason-20261107031743`).


## v4.7 (2026-11-08, app 1.4.7+14): minutes at the shop, daily pin target

**«الوقت المستغرق عند المحل» (required)**
- Pin form: required number of minutes (1 … 600, Arabic digits accepted), shown in the pin details, «محلاتي», the dashboard's «المحلات المحتملة» (list, map popup, details) and its Excel export.
- Server: `Prospects.MinutesSpent` (migration `0009_ProspectMinutesSpent.sql`). Required only for apps that send `X-App-Version` ≥ 1.4.7 (the app sends it on every request from 1.4.7), so 1.4.6 keeps working until it is forced to update. An old app's edit keeps the stored minutes.

**«الهدف اليومي» (daily pin target)**
- Default target for every agent in «إعدادات التطبيق» (0 = off); an agent's own target from their «سجل الهدف اليومي» (dashboard → «الأهداف اليومية» or «المسوّقون» → row menu). `AppSettings.DailyPinTarget`, `Users.DailyPinTarget` (NULL = default).
- App «ملخصي»: «هدف اليوم» card with progress (`me/overview`: `dailyPinTarget`, `pinsToday`).
- Results: met / missed / off (did not work and pinned nothing). Finished Iraq days are frozen into `agents.PinTargetDays` with the target in force that day (WorkDayCloseWorker → `PinTargetService.CloseDaysAsync`, marker `AppSettings.PinTargetLastClosedDay`); today is live. The record starts on deployment day (2026-11-08); earlier days are not judged. Migration `0010_DailyPinTargets.sql`.
- Dashboard: new page «الأهداف اليومية» (`customer-app-targets.vue`): any day, counts of met / missed / off, the agents' list; each agent opens `CaTargetHistoryDialog.vue` (last 90 days, met / missed / off counts, own target). API: `GET admin/targets?date=`, `GET admin/agents/{id}/targets`, `PUT admin/agents/{id}/target`.
- Deployed 2026-11-08: server backup `CustomerApp.prev-targets-20261108032122`, dashboard backup `AdminFrontend.prev-targets-20261108032408`. APK `demo/apk/qalaa-agents-live-v4.7.apk` **published 2026-11-08 03:28 as the mandatory version** (`deploy/publish-apk.sh`).
- Note: on 2026-11-08 another task («supervisor access», backup `CustomerApp.bak-supervisor-access-20261108_031842.tar.gz`) deployed this server from the same folder at 03:17, which applied migration 0010 early; its `Auth/AuthSetup.cs` change is included in this deploy.

## v4.8 (2026-11-09, app 1.4.8+15): «المحلات» tab replaces «ملخصي»

- Tabs: طلب بيع · المحلات · الطلبات · الدوام · حسابي. `/home` and `/pins` redirect to `/stores`; `features/home` was removed (its work-day card already lives on «الدوام»; the notifications bell moved to «المحلات»).
- «المحلات» (`StoresScreen` in `work/presentation/my_pins_screen.dart`): the agent's pinned shops for a range of Iraq days — chips «اليوم» (default) · «أمس» · «آخر 7 أيام» · «هذا الشهر» · «تاريخ محدد» (date-range picker) — with the count, a search (top bar), today's «هدف اليوم» as «2 من 5» (a slash reads backwards in Arabic) and the «تثبيت محل» button. A shop opens its details and map (`/pins/:id`).
- Server: `GET prospects/mine?from=&to=` (Iraq days, both included). Deployed 2026-11-09 (backup `CustomerApp.prev-stores-20261109081331`).
- **1.4.8 published 2026-11-09 as the mandatory version** (`deploy/publish-apk.sh`).

## v4.9 (2026-11-14, app 1.4.9+16): «المحلات المثبّتة», work periods, time target

**App**
- Pin form: only the location, shop name and interest. Removed: type/description, phone, goods value, size and «الوقت المستغرق عند المحل». Editing an older pin keeps what it already had.
- A new pin also sends where the phone was (`agentLatitude/agentLongitude`), so the dashboard knows when the pin was moved on the map.
- «الدوام» no longer has «بدء الدوام» / «استئناف الدوام» or the morning prompt. The day starts or resumes only by pinning a shop or sending a sale request; the agent can still end it. When not working, the button reads «ثبّت محلاً لبدء الدوام».
- «هدف اليوم» is gone from «المحلات». The agent never sees a target or the periods.

**Work periods (server, `Domain/Work/PinPeriods.cs`)**
- A shop's time = the time since the previous pin. A pin less than 3 minutes after the previous one is flagged.
- 15 minutes or more without a pin ends a period at the last pin; the next pin starts a new period. The gap is not counted.
- Work time for the day = the sum of the periods (first to last pin of each). Iraq days.

**Daily target = work time**
- Default in «إعدادات التطبيق» (hours, stored as minutes in `AppSettings.DailyWorkMinutesTarget`, 0 = off) and an agent's own target from «السجل» (`Users.DailyWorkMinutesTarget`, NULL = default).
- Finished days are frozen in `agents.WorkTargetDays` (target, work minutes, pins, periods, met). The record starts on 2026-11-14; the old pin-count target data (`DailyPinTarget`, `PinTargetDays`) is kept but unused.
- `me/overview` always returns `dailyPinTarget = 0`, so 1.4.7–1.4.8 hide their old target card.
- Migration `0011_WorkTimeTargets.sql`.

**Dashboard**
- «المحلات المحتملة» is now «المحلات المثبّتة» (`customer-app-prospects.vue`). It opens on today (Iraq time) and shows nothing until an agent is picked (`?userId=&date=` in the URL). It shows work time against the target, pins, periods, first pin and quick pins; a day bar with the periods; and the list grouped by period with each shop's time, quick pins highlighted and the breaks between periods. Today refreshes every minute.
- Clicking a shop (list, bar or map) hides the other pins and zooms in. If the pin is more than 25 m from where the agent stood, a blue dot labelled «موقع المسوّق عند تثبيت هذا المحل» and a dashed line are shown.
- «الأهداف اليومية» and the agent's «سجل الهدف اليومي» show work time against the time target; a map-pin button opens that agent's day in «المحلات المثبّتة».
- API: `GET admin/prospects/timeline?userId=&date=`; `PUT admin/agents/{id}/target { dailyWorkMinutesTarget }`; settings field `dailyWorkMinutesTarget`.

**Deployed 2026-11-14** (server + dashboard). Backups: `/opt/qalaa-runtime/CustomerApp.prev-pintimeline-20261114194938`, `/opt/qalaa-runtime/AdminFrontend.prev-pintimeline-20261114195258`. APK `demo/apk/qalaa-agents-live-v4.9.apk` (same signing key) **published 2026-11-14 as the mandatory version** (`deploy/publish-apk.sh`).

## 2026-11-15: 1.4.9 could not reach the server → 1.4.10 (app 1.4.10+17)

- **Cause:** 1.4.9 was built with a plain `flutter build apk --release`, without `--dart-define=API_BASE_URL=http://agents.alsaaeidy.com/api/v1/`, so it used the code's placeholder default `https://api.qalaa-store.iq/api/v1/`. Every phone that took the mandatory 1.4.9 update lost the server. The server itself was fine.
- **Fix:** `AppConfig.prodDefaultBaseUrl` is now `http://agents.alsaaeidy.com/api/v1/`, so a release build without the define still reaches the live API. 1.4.10 was built with the define as well; its `libapp.so` was checked to contain only `http://agents.alsaaeidy.com/api/v1/`. Same signing key. APK `demo/apk/qalaa-agents-live-v4.10.apk`.
- **Phones already on 1.4.9 cannot learn about 1.4.10** (they cannot reach `app/config`). They must install it by hand from `http://admincompany.alsaaeidy.com/mobile-updates/agents/qalaa-agents-latest.apk` once it is published. It installs over 1.4.9 and keeps the sign-in. Phones still on 1.4.8 get it through the normal forced-update screen.
- **Before publishing any agents APK:** check the server address inside it, e.g. `unzip -p <apk> lib/arm64-v8a/libapp.so | grep -aoE "https?://[^\"]*alsaaeidy[^\"]*" | sort -u`.
