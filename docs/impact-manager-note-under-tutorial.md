# Impact: إرسال ملاحظة إلى المدير (حيدر) — تحت الفيديو التعليمي

## Scope
Notes go to **المدير** (UserType المدير / حيدر), NOT المدير المفوض / CentralComplaints.

| Surface | Change |
|---------|--------|
| BE_SalesEmployee | `dbo.DirectorManagerNotes` + `api/internal/director-notes` + `api/director/notes*` (Director JWT) |
| BE_Delegate | `POST api/delegate/director-notes`, `POST api/Followers/director-notes` → gateway |
| BE_Company | `POST api/branch-manager/director-notes` → gateway |
| FE_Company | page `director-notes` + nav for المدير only |
| Flutter ×3 | note button under tutorial → new director-notes endpoints |
| ShortFrontend | Untouched |
| mobile-updates / OTA | **Not published** — USB install only |

## Versions (local install)
- Delegate `1.0.37+67`
- Follower `1.0.29+33`
- Branch manager `1.0.26+27`

## Regression
- DM complaint inbox unchanged
- Director tutorial/ratings unchanged
- Note ≥10 chars reaches Director inbox only
