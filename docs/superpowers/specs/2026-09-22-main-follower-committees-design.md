# Design: المتابع الرئيسي — لجان الرقابة الميدانية

**Date:** 2026-09-22  
**Status:** Approved by user direction (4 phases; implement phase-by-phase with tests; no further clarifying questions)

## Problem

المدير المفوض يشكّل **لجاناً** من **متابعين رئيسيين** ويكلّفهم بمحافظة أو أكثر (متغيّرة عبر الزمن). اللجنة تراقب المبيعات وتقييمها، وتقيّم المندوب / المتابع الفرعي / موظف المبيعات. التقييمات تبقى في سجل دائم عند إعادة التكليف لنفس المحافظة لاحقاً.

لا يوجد اليوم مفهوم «لجنة» أو «متابع رئيسي» في المستودع ([inventory](../plans/) — أقرب: `follower_application` لمصادقة 1–10، و`sales_filter` للمحافظات المتعددة، و`delegated_manager_application` للتكليف المركزي).

## Goals

1. تكليف لجان متعددة المحافظات من المدير المفوض.
2. تطبيق Flutter للمتابع الرئيسي يعمل عبر المحافظات المكلَّف بها فقط.
3. تقييم مبيعات 1–10 مع قواعد ملاحظة.
4. تقييم موظفين (مندوب، متابع فرعي، موظف مبيعات) مع ملاحظات تظهر في سجلهم.
5. سجل تاريخي دائم لكل مهمة/محافظة.

## Non-goals (out of V1 phases 1–4)

- استبدال تطبيق المتابع الفرعي الحالي.
- تقييم العملاء بمقاييس قانونية/سداد الموجودة في مدير الفرع.
- Offline-first كامل (يُذكر فقط كتحسين لاحق بعد المرحلة 4 إن لزم).

## Four phases

| Phase | Deliverable | Done when |
|-------|-------------|-----------|
| **1 — لجان ومهام** | هوية متابع رئيسي + جداول لجنة/مهمة + API مدير مفوض (إنشاء لجنة، اختيار متابعَين، تكليف محافظات) + تطبيق Flutter: تسجيل دخول + عرض المهام والمحافظات | اختبارات BE + شاشة مهام تعمل على ديمو |
| **2 — مراقبة مبيعات وتقييمها** | قراءة مبيعات المحافظات المكلَّفة عبر البوابة + تقييم 1–10 (ملاحظة إجبارية إذا ≥7؛ اختيارية إذا ≤6) + حفظ في سجل المهمة | اختبارات قواعد التقييم + تدفق تقييم مبيع واحد |
| **3 — تقييم الموظفين** | تقييم مندوب لكل قائمة، متابع فرعي، موظف مبيعات (كلّها /10) + كتابة ملاحظة على سجل الهدف | اختبارات الكتابة على الملاحظات + UI تقييم |
| **4 — السجل وإعادة الزيارة** | عرض تاريخ تقييمات لجنة سابقة لنفس المحافظة؛ تقارير ملخص للمفوض؛ صقل UX وإنتاج APK | اختبار إعادة فتح مهمة قديمة + APK ديمو |

## Architecture (Clean Architecture)

```
┌─────────────────────────────────────────────────────────┐
│ Flutter: main_follower_application                       │
│  presentation → application (use cases) → domain         │
│  adapters: api_gateway, local_session                    │
└─────────────────────┬───────────────────────────────────┘
                      │ JWT (central, multi-city)
┌─────────────────────▼───────────────────────────────────┐
│ BE_SalesEmployee (gateway)                               │
│  MainFollower / Committee module (central SQL store)     │
│  Fan-out to branch BE_Company / BE_Delegate for sales    │
└─────────────────────┬───────────────────────────────────┘
                      │
        ┌─────────────┴──────────────┐
        ▼                            ▼
 BE_Company (branch)          BE_Delegate (branch)
 sales read / notes           follower lists / delegates
```

### Why central gateway store for committees

التكليف متعدد المحافظات ومتغيّر؛ نفس نمط استثناءات المدير المفوض وفلترة المبيعات (`AllowedFilterCities`). بيانات اللجنة والمهمة والسجل تعيش مركزياً؛ قراءة المبيعات تتم عبر fan-out للفرع عند الحاجة.

### Identity

- نوع مستخدم جديد: `متابع رئيسي` (أو claim `MainFollower` على JWT البوابة).
- تسجيل الدخول: بوابة مركزية (مثل المدير المفوض / موظف الفلترة) — **بدون** اختيار محافظة واحدة عند الدخول؛ المحافظات تأتي من المهام النشطة.
- المدير المفوض ينشئ اللجان عبر APIs محمية بـ `DelegatedManager`.

### Domain model (core)

- **Committee** — رقم/اسم، أعضاء (2+ متابع رئيسي)، أنشأها مفوض، حالة.
- **Mission** — لجنة + مجموعة `CityValue` + فترة/حالة (Active/Completed) + ملاحظة تكليف.
- **SaleReview** — MissionId + CityValue + SaleId + Score 1–10 + Note + ReviewerUserId + AtUtc.
- **StaffReview** — MissionId + TargetType (Delegate|SubFollower|SalesEmployee) + TargetId + CityValue + ListId? + Score 1–10 + Note + AtUtc.

### Rating rules (phase 2+)

- Score ∈ [1, 10].
- Note **required** when score ≥ 7.
- Note **optional** when score ≤ 6.
- Staff reviews always attach a durable note entry on the target’s notes stream (phase 3).

### UI/UX direction (Flutter)

- Arabic RTL، خط Cairo (مثل تطبيقات الحقل الحالية).
- الشاشة الرئيسية: المهام النشطة → بطاقات محافظات.
- داخل المحافظة: تبويب مبيعات | مندوبون | متابعون | موظفو مبيعات.
- سجل المهمة: خط زمني للتقييمات (مرحلة 4 يعزّز العرض).

## Impact

| Surface | Change |
|---------|--------|
| `BE_SalesEmployee` | Module + SQL central + DM APIs + main-follower auth |
| `delegated_manager_application` | شاشات إدارة اللجان/التكليف |
| `main_follower_application` (جديد) | تطبيق المتابع الرئيسي |
| `BE_Company` / Flutter أخرى | قراءة فقط عبر البوابة في المراحل 1–2؛ ملاحظات في 3 |
| DB | جداول مركزية على اتصال البوابة (ليس كل فرع) |

## Testing gate per phase

لا تبدأ المرحلة التالية قبل:

1. اختبارات وحدة للنطاق/القواعد الجديدة خضراء.
2. تحقق يدوي أو سكربت ديمو للمسار الأساسي للمرحلة.
3. لا انحدار على Login المفوض / بوابة المبيعات الحالية.

## Approaches considered

1. **(مختار)** تطبيق Flutter جديد + متجر مركزي على البوابة + fan-out للفروع.  
2. توسيع `follower_application` بدور جديد — يخلط متابع فرعي (فرع واحد) مع دور متعدد المحافظات.  
3. تخزين اللجنة داخل كل فرع — يكسر التكليف متعدد المحافظات والسجل الموحد.

التوصية: (1).
