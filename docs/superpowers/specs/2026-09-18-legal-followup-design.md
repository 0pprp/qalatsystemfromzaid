# Legal Follow-up (القانونية) — Design Spec

**Date:** 2026-09-18  
**Status:** Approved by product direction (لوحة فقط، بدون أسئلة إضافية)

## Goal

من تبويب **غير مسند** في طلبات البيع: بدل إسناد الطلب لموظف المبيعات، يُرسل للمتابع لإنشاء/مراجعة كشف قانونية (الاسم، الهاتف، العنوان، وGPS/صورة المحل/المستندات إن وُجدت من بروفايل الزبون). حالات المتابعة: قيد العمل / تمت المتابعة.

## Decisions

| Decision | Choice |
|----------|--------|
| من يرسل؟ | لوحة FE_Company فقط — طلب غير مسند (`Status=New`) |
| بدل ماذا؟ | بدل «إرسال للموظف» (مسار البيع) |
| أين البيانات؟ | جدول فرعي على `DatabaseCompany*` |
| مصدر الحقول | `SalesRequests` + مستندات/محل الزبون إن وُجدت |
| تطبيق المتابع | `BE_Delegate` → `Followers/LegalFollowups*` |
| لوحة | تبويب `القانونية` + صفحة القانونية تحت إدارة المبيعات |

## API (canonical)

### BE_Company (FE)

- `GET /api/sales-manager/legal-followups?status=InProgress|Completed`
- `GET /api/sales-manager/legal-followups/{id}`
- `GET /api/sales-manager/legal-followups/followers`
- `POST /api/sales-manager/legal-followups` `{ salesRequestId, followerUserId, note?, customerName?, customerPhone?, customerAddress?, existingCustomerId? }`

### BE_Delegate (Follower app)

- `GET /api/Followers/LegalFollowups?status=`
- `GET /api/Followers/LegalFollowups/{id}`
- `PUT /api/Followers/LegalFollowups/{id}/complete` `{ note? }`

## Data

`dbo.LegalFollowupCases`: Id, SaleId (nullable), SalesRequestId, CustomerId, FollowerUserId, FollowerName, Status, CustomerName, CustomerPhone, CustomerAddress, Latitude, Longitude, ShopImageKey, CreatedByUserId, CreatedByName, CreatedAtUtc, CompletedAtUtc, Note, CompletionNote.

عند الإرسال: طلب البيع → `Status=LegalFollowup`.

## Out of scope

- إرسال من المبيعات المكتملة
- إرسال من تطبيق موظف المبيعات
- تغيير IsLegalCustomer على العميل إلا إذا طُلب لاحقاً
