# Design: دور المراقب + إدارة المتابعين

## Decisions

- Account: central gateway config `MonitorAccount` / `MonitorAccounts` (like المدير).
- `UserType` JWT: `المراقب`.
- UI: only «عمل المتابعين» (6 tabs) + follower create/edit/delete.
- Ops writes: full (including collection toggle), same as المدير.
- Director panel also gets follower CRUD in the same page.
- Follower rows live on branch `dbo.Users` (`متابع`) + `FollowerUserLists`; gateway proxies with `X-Sales-Gateway-Key` via `internal/follower-users/*`.

## Non-goals

- Monitor access to ratings / summary / notes / tutorials / sales requests.
- Creating Monitor from users-list / accountant panel.
