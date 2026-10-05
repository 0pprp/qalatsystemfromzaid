# Follower sales authentication + monthly stats

Date: 2026-09-20  
Status: approved by user

## Scope
- Side drawer (☰ right): المبيعات (pending auth), الإحصائيات, المتابعة الميدانية
- Pending queue: sales posted to list Customers, not yet authenticated
- Detail: profile, shop info, KYC docs, shop photo
- Authenticate: rating 1–10; note optional if ≥7 else required
- Profile section «بيانات المتابع»: date, authenticator name, rating, note
- Stats per list container: new customers count; added amount = live list total − prev month-end snapshot; installment delta same pattern; freeze on new month
- APK install fix + mandatory update after tests

## Backend (BE_Delegate)
- Tables: FollowerSaleAuthentications, FollowerListMonthlySnapshots (+ EnsureSchema)
- APIs under Followers/: pending sales, sale detail media, POST authenticate, GET stats, profile includes follower auth block
- Impact: BE_Delegate only for new APIs; Flutter follower app; DB auto-schema; no FE_Company/BE_Company change unless media paths need company files (reuse existing media)

## Flutter
- Drawer on HomePage; remove field followups button from home row
- New pages + profile section
- Install: OpenFilex path aligned with delegate + permission settings
