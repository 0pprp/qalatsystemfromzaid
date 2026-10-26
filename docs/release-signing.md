# Release signing policy (F14)

**Do not change the signing certificate for an already-published `applicationId` without an upgrade plan.**

## Current policy (code)

- `delegate_application` release builds **fail** if `android/key.properties` is missing.
- Silent fallback to the debug keystore is removed.
- `applicationId` stays `com.example.delegate_application` so in-place updates keep local SQLite.

## Continuity with previously shipped APKs

| Situation | Safe action |
|---|---|
| Field APKs already signed with a known release keystore | Point `key.properties` at that same keystore |
| Field APKs were debug-signed on a build machine | Export that debug keystore into `key.properties` as the release key **only if** that is the key devices already trust |
| Must rotate to a new certificate | Requires new `applicationId` OR Play App Signing migration; users may need reinstall → **local payment queue at risk** |

## Other Flutter apps

Apps still using `signingConfig = debug` for release are tracked as **open F14** until each gets `key.properties` + fail-closed release. Do not flip them all at once without inventory of installed signatures.

## Checklist before first fail-closed release

1. Inventory installed APK signatures (`apksigner verify --print-certs`).
2. Confirm `storeFile` / alias match field devices.
3. Smoke: install over previous APK without uninstall; open app; confirm pending payments still present.
4. Rollback: keep previous APK + keystore backup offline.
