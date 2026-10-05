# Branch Manager mobile app — Phase 1

Date: 2026-09-21  
Status: approved (user: نفذ بدون أسئلة — حساب من المستخدمين، موبايل، أمان + UI ممتاز)

## Phase 1 scope
- New Flutter app `branch_manager_application` (مدير الفرع)
- Login with same Users accounts created by المحاسب الرئيسي (UserType = مدير فرع)
- Secure session (token/session like company admin path; no secrets in logs)
- Tabs: القرارات (WeakWeekPayers + Decide), سجل القرارات
- Mandatory note for متواصل; قانونية / وهمي per existing APIs
- In-app update gate (`branch-manager` app key)
- Polish UI/UX (RTL Cairo, clear hierarchy, one job per screen)

## Out of scope (later phases)
- Customers notes search, private staff notes, CEO request rename, sales feed, charts

## Backend
- Prefer existing: CustomerDecisions WeakWeekPayers/Decide/history
- Extend only if mobile auth needs a dedicated endpoint for BM login on city API

## Test
- Najaf demo: login as BM → see weak payers → decide → verify IsLegal / snooze week
