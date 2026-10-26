# Flutter test baseline (phase 3)

Date: 2026-10-05. Branch: `security/audit-remediation-f01-f20`.

## Per-package results (targeted runs)

| Package | Command | Result | Root cause / fix |
|---|---|---|---|
| `delegate_application_aqeel` | `flutter test test/widget_test.dart` | **1 passed** | Counter template replaced with `MyApp` → welcome route (`جاري التحميل...`); mock `SharedPreferences`; flush welcome timer on teardown |
| `MA_ShortCompany` | `flutter test test/widget_test.dart` | **2 passed** | Use `ShortCompanyApp`; `FakeWebViewPlatform` (no native channels); `loadRequest` after `_controller` assign; splash + error-retry smoke |
| `delegated_manager_application` | `flutter test test/home_shell_test.dart` | **2 passed** | Test expected legacy `الإحصائيات`; UI contract is `التقييمات` in `home_shell.dart` `_menu` |
| `mobile_app` | `flutter test test/widget_test.dart` | **1 passed** | Pending Dio timers from `LoginScreen._fetchCities`; inject `loginCitiesLoader` via `ManagementApp` / `AppRoutes`; `SharedPreferences.setMockInitialValues`; avoid `pumpAndSettle` (logo animation) |
| `delegate_application` | `flutter test test/payment_sync_mutex_test.dart test/delegate_data_refresh_test.dart` | **9 passed** | Mutex + fetch tests hit production `PaymentSyncService` / `DelegateDataRefreshService` via `@visibleForTesting` hooks (not local fake mutex) |

## Harness notes

- **WebView unit tests** cannot pump real platform WebViews; use `webview_flutter_platform_interface` fakes and document that device/E2E still validates real WebView.
- **Payment / refresh services** expose `reset*TestHooks()`, `isOnlineOverride`, and (refresh) `httpGetOverride` for deterministic tests without weakening sync gates or SQLite replace rules.

## Untested / open

- Full `flutter test` for every Flutter app in one CI pass
- Release APK builds (require `key.properties`)
- Real-device WebView load and signing continuity
