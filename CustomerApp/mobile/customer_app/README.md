# قلعة الضمان — Customer app (Flutter, Android + iOS)

Arabic-first (RTL, `ar-IQ`) shopping app for the customers of «قلعة الضمان»: products for cash or
daily installments, orders, installment accounts («مشترياتي وأقساطي»), and the referral/commission
program («أرباحي» / «شبكتي»). The app talks only to `BE_CustomerApp`. The API contract is in
`../../docs/API_CONTRACT.md`, sections 1–8.

## Requirements

- Flutter 3.47+ (Dart 3.13+). This project was built with Flutter 3.47.9.
- Android SDK 36, JDK 17. For iOS: Xcode 16+ and CocoaPods, on macOS.

## Run

```bash
flutter pub get

# Demo mode: in-memory fake backend with Arabic sample data. No server needed.
flutter run --dart-define=USE_FAKE_API=true

# Local backend. The debug default is http://10.0.2.2:5380/api/v1/ (the Android emulator's alias for your PC).
flutter run

# Real device on the same Wi-Fi as your PC
flutter run --dart-define=API_BASE_URL=http://192.168.1.10:5380/api/v1/
```

### Demo accounts (fake mode)

The OTP code is always **`123456`**.

| Phone | What it shows |
|---|---|
| `07701234567` | Registered user with 3 completed sales. «إنشاء حساب لشخص آخر» is locked (3 of 5). |
| `07801234567` | Registered user with 6 completed sales. The create-account-for-another flow is unlocked. |
| any other valid number (e.g. `07501112233`) | The first-time registration flow. Any 6-character referral code is accepted. |

Fake mode also shows: a `partial` warning in installments (البصرة "unavailable"), the 409
`duplicate_open_order` dialog (order the same product twice with the same phone), Idempotency-Key
replay, payout requests, and notifications with deep links.

### dart-defines

| Key | Default | Purpose |
|---|---|---|
| `API_BASE_URL` | debug `http://10.0.2.2:5380/api/v1/`, release `https://api.qalaa-store.iq/api/v1/` | Backend base URL (a trailing `/` is added automatically) |
| `USE_FAKE_API` | `false` | Use the in-memory fake backend (`lib/core/fake`) |
| `STORE_WEB_URL` | `https://store.qalaa.iq` | Web store, used for fallback share links |
| `APP_STORE_ID` | empty | Numeric App Store id, used by the iOS "update now" button |

## Architecture

```
lib/
  main.dart / app.dart        bootstrap, MaterialApp.router, RTL, theme
  core/
    config/                   AppConfig (dart-defines)
    network/                  ApiClient (dio), AuthInterceptor (bearer + refresh on 401), ApiException (Arabic error mapping), JSON helpers, Paged<T>
    storage/                  TokenStorage (flutter_secure_storage)
    router/                   go_router: 5-tab StatefulShellRoute, auth guard, deep links /p/:id, /r/:code, /orders/:id
    push/                     PushService (firebase_messaging + flutter_local_notifications, optional)
    deep_links/               app_links → routes (https://store.qalaa.iq/... and qalaa://...)
    fake/                     FakeBackend (dio interceptor implementing the contract) + sample data
    theme/ utils/ widgets/    design system, formatters (money "650,000 د.ع", Iraqi month names), validators, skeletons, empty/error states, paged list
  features/<feature>/{data,presentation}
    catalog  auth  cart  orders  purchases  earnings  network  notifications  account  app_config  shell
```

- State: `flutter_riverpod` 3. Automatic provider retry is turned off, and screens show an explicit retry button instead.
- Each feature has its models (`fromJson`, tolerant of missing fields) and a repository in `data/`, and its screens in `presentation/`.
  To add a future module (merchants, suppliers, …), create `lib/features/merchants/{data,presentation}`,
  add a repository provider that uses `apiClientProvider`, and register its routes in `core/router/app_router.dart`.
- Fake mode is a Dio interceptor, so fake mode runs the same repositories and parsers as production.

## Firebase (push notifications) — optional

The app builds and runs **without** Firebase files. `Firebase.initializeApp()` is wrapped in try/catch,
and push turns itself off when the files are missing. To turn push on:

1. Create a Firebase project and add these two apps:
   - Android package `iq.qalaa.customer_app`
   - iOS bundle id `iq.qalaa.customerApp` (see the note on the bundle id below)
2. Android: put `google-services.json` in `android/app/`. Then uncomment the
   `com.google.gms.google-services` line in **both** `android/settings.gradle.kts` and `android/app/build.gradle.kts`.
3. iOS: add `GoogleService-Info.plist` to `ios/Runner/` through Xcode (Runner target → "Add Files"). Then:
   - In Signing & Capabilities, add **Push Notifications** and **Background Modes → Remote notifications**.
     `UIBackgroundModes` is already set in Info.plist.
   - Upload your APNs auth key (.p8) in Firebase → Project settings → Cloud Messaging.
4. (Alternative) Run `flutterfire configure`, then pass the generated `DefaultFirebaseOptions.currentPlatform`
   to `Firebase.initializeApp` in `lib/core/push/push_service.dart`.

Behaviour:
- The app asks for notification permission **after login**, not at first launch.
- After login it registers the token with `POST me/devices`, and again whenever the token refreshes.
- On logout it sends the push token in `auth/logout`.
- A tap on a notification opens `/orders/{orderId}`, then `/p/{productId}`, else `/notifications`, following the FCM `data` payload `{ type, notificationId, orderId?, productId? }`.
- Android channel id: `qalaa_default`.

## Deep links

- Android App Links for `https://store.qalaa.iq/p/*`, `/r/*`, `/orders/*` (`autoVerify`), and the custom scheme `qalaa://`.
  Publish `https://store.qalaa.iq/.well-known/assetlinks.json` with your release SHA-256 certificate fingerprint.
- iOS: the custom scheme `qalaa://` is already registered. For universal links:
  - Add the **Associated Domains** capability with `applinks:store.qalaa.iq`.
  - Publish `apple-app-site-association` on the web store.
- `/r/CODE` saves the referral code and pre-fills it in the registration form.

## Release signing (Android)

```bash
keytool -genkey -v -keystore ~/qalaa-upload.jks -keyalg RSA -keysize 2048 -validity 10000 -alias upload
```

Create `android/key.properties`. It is git-ignored, so never commit it.

```
storePassword=...
keyPassword=...
keyAlias=upload
storeFile=C:/path/to/qalaa-upload.jks
```

```bash
flutter build appbundle --release --dart-define=API_BASE_URL=https://api.qalaa-store.iq/api/v1/
```

Without `key.properties`, release builds fall back to the debug key. Never upload those builds.
Turn on Play App Signing, then keep the upload key and its passwords in a safe place.

iOS:

```bash
flutter build ipa --release --dart-define=API_BASE_URL=https://api.qalaa-store.iq/api/v1/ --dart-define=APP_STORE_ID=XXXXXXXXXX
```

Set the team and signing in Xcode first.

## Versioning and forced update

Bump `version:` in `pubspec.yaml` for every release (`1.0.1+2`: name+build).
On launch the app reads `GET app/config`:
- If the installed version is below `minSupportedVersion`, a blocking "update" screen appears.
- If it is below `latestVersion`, a soft snackbar appears.

## Store submission checklist

**Both stores**
- [ ] Production `API_BASE_URL`; `USE_FAKE_API` is not set.
- [ ] `version` bumped. App name «قلعة الضمان». Final launcher icon: replace `mipmap-*` and `AppIcon.appiconset`, e.g. with `flutter_launcher_icons` using `assets/images/logo.png`.
- [ ] Terms and privacy URLs live (`app/config.termsUrl` / `privacyUrl`). The privacy policy covers phone, name, address, optional location, push token and referral data.
- [ ] Account deletion works in the app (حسابي → حذف الحساب). Also give a web deletion URL in the store forms.
- [ ] Reviewer demo account: a phone number plus a fixed OTP that the backend accepts for review only.
- [ ] Arabic screenshots (6.7" iPhone, 6.5" iPhone, Android phone) of: home, product, order wizard, orders, installments, earnings.

**Google Play**
- [ ] Target SDK: the latest (`flutter.targetSdkVersion`). Permissions are only INTERNET, POST_NOTIFICATIONS, and COARSE/FINE location (foreground, requested on tap). There is **no SMS permission**: OTP autofill uses `AutofillHints.oneTimeCode`.
- [ ] Data safety form: phone number, name, approximate/precise location (optional, order address), app interactions, device id (FCM token). Data is encrypted in transit, and users can request deletion.
- [ ] Content rating questionnaire. Declare the financial features (installment purchases; commissions are a referral reward, not an investment product).
- [ ] `assetlinks.json` published for App Links.

**App Store**
- [ ] Bundle id `iq.qalaa.customerApp`. **Apple does not allow `_` in bundle ids**, so the requested `iq.qalaa.customer_app` cannot be used on iOS. Android uses `iq.qalaa.customer_app`.
- [ ] Info.plist: Arabic `NSLocationWhenInUseUsageDescription` (present). No ATT / IDFA (the app does not track). `ITSAppUsesNonExemptEncryption = false`.
- [ ] 4.5.4: marketing pushes are opt-in. The registration checkbox is unchecked by default, and there is a toggle in حسابي. The backend sends `Promotion` only to opted-in users.
- [ ] 5.1.1(v): in-app account deletion with typed confirmation. It warns that unpaid earnings are forfeited.
- [ ] App Privacy nutrition labels match the Play data-safety answers.
- [ ] Push capability + APNs key, if push is enabled.

## Quality

```bash
flutter analyze
flutter test
flutter build apk --debug --dart-define=USE_FAKE_API=true
```

Tests cover:
- the phone validator and normalisation
- money and date formatting
- order wizard validation (`OrderDraft`)
- order status mapping and the timeline
- deep link and push routing
- version comparison and error mapping
- the repositories against the fake backend (409 duplicate, idempotency, payouts)
- the OTP and phone widgets
- an app smoke test
