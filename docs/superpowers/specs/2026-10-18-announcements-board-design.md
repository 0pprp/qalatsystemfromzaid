# Announcements Board — Design

**Date:** 2026-10-18  
**Status:** Approved for implementation

## Goal

Director (حيدر) manages text/image ads; delegate and follower apps show a modern top-of-home banner. Multiple ads auto-rotate every 3s; a single ad stays static. Optional link via «اضغط هنا».

## Decisions

- **Storage:** Central Sales Gateway SQL (`dbo.AppAnnouncements`), same pattern as tutorial videos.
- **Audience:** Dropdown `delegate` | `follower` (one per ad). No city filter — all users of that app.
- **Content:** Exactly one of `text` | `image`. Images uploaded from director device to gateway disk.
- **Link:** Optional for both text and image (`HasLink` + `LinkUrl`).
- **Placement:** Top of home (under header), both apps.
- **Deploy constraint (this delivery):** Publish GW + director FE only. Install Flutter APKs on the owner's device; do **not** publish mobile OTA/store updates.

## API

| Who | Route | Auth |
|-----|--------|------|
| Director CRUD | `api/sales-manager/announcements` | Director |
| Image upload | `api/sales-manager/announcements/upload-image` | Director |
| Mobile list | `api/mobile-announcements/{appKey}` | Anonymous |
| Media file | `api/mobile-announcements/media/{fileName}` | Anonymous |

## Mobile behaviour

- 0 ads → hide banner  
- 1 ad → static card  
- 2+ → PageView / auto-page every 3 seconds  
- `HasLink` → button «اضغط هنا» opens URL  

## FE

Tab «الإعلانات» for المدير only (nav + router allowlist), next to tutorial videos.
