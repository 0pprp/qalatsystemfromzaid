# Repository folders sheet / ورقة مجلدات المستودع

What each top-level folder on GitHub `main` is for.  
شرح كل مجلد رئيسي على فرع `main` بالعربي والإنجليزي.

Repo: `https://github.com/0pprp/qalatsystemfromzaid` · branch: **`main`**

---

## Sheet / الجدول

| Folder | English | العربية |
|--------|---------|---------|
| **BE_Company** | Main company/admin API (.NET). Branch databases, sales module, ratings, CRM sales requests, Excel customer search, legal follow-up, waivers, director ops. | الـ API الرئيسي للشركة/الإدارة (.NET). قواعد فروع، موديول المبيعات، التقييمات، طلبات البيع CRM، بحث إكسل الزبائن، المتابعة القانونية، التنازلات، عمليات المدير. |
| **BE_Company.Sales.Tests** | Unit/integration tests for the Sales module inside BE_Company. | اختبارات وحدة/تكامل لموديول المبيعات داخل BE_Company. |
| **BE_Company.Tests** | Broader BE_Company tests (payments, portal, followers contracts, etc.). | اختبارات أوسع لـ BE_Company (دفعات، بوابة، عقود المتابعين…). |
| **BE_SalesEmployee** | **Sales Gateway**: aggregates multi-branch calls, sales-manager APIs, delegated-manager / director / mobile announcement & task services. FE and mobiles talk here first. | **بوابة المبيعات**: تجميع استدعاءات الفروع، واجهات مدير المبيعات، خدمات المدير المفوض/المدير/الإعلانات والمهام للموبايل. الواجهة والموبايل يتصلان هنا أولاً. |
| **BE_SalesEmployee.Tests** | Tests for the Sales Gateway and delegated-manager services. | اختبارات بوابة المبيعات وخدمات المدير المفوض. |
| **BE_DelegateWebApplication** | Delegate / follower field API (collections, payments, follower lists, day ratings). Used by delegate & follower mobile apps. | API المندوب/المتابع الميداني (جباية، دفعات، قوائم، تقييم يومي). تستخدمه تطبيقات المندوب والمتابع. |
| **FE_Company** | Main Vue admin frontend (Vuexy). Sales manager board, Excel search page, director screens, accountant, monitors. | الواجهة الإدارية الرئيسية Vue (Vuexy). لوحة مدير المبيعات، صفحة بحث الإكسل، شاشات المدير، المحاسب، المراقبين. |
| **FE_ShortCompany** | Shorter/alternate company frontend (subset of admin UI). | واجهة شركة مختصرة/بديلة (جزء من شاشات الإدارة). |
| **CustomerPortal** | Customer-facing portal assets/pages (tokenized customer access). | بوابة الزبون (وصول ب토كن للزبون). |
| **DatabaseScripts** | SQL scripts: schema, stored procedures, deploy/demo scripts for branches. Needed to understand DB shape. | سكربتات SQL: مخطط، إجراءات مخزنة، نشر/تجريبي للفروع. مهمة لفهم شكل قاعدة البيانات. |
| **docs** | Project documentation (API notes, designs, this sheet, audits). Start here as a collaborator. | توثيق المشروع (ملاحظات API، تصاميم، هذه الورقة، تدقيقات). ابدأ من هنا كمساهم. |
| **.github** | CI workflows (e.g. GitHub Actions). Not application runtime. | سير عمل CI (مثل GitHub Actions). ليست وقت تشغيل للتطبيق. |
| **sales_employee_application** | Flutter app for **sales employees** (submit/manage sales requests in the field). | تطبيق Flutter لـ **موظفي المبيعات** (تقديم/إدارة طلبات البيع ميدانياً). |
| **sales_filter_application** | Flutter app historically used for sales filtering / intake (legacy path; see decommission docs). | تطبيق Flutter كان لفلترة/استقبال المبيعات (مسار قديم؛ راجع وثائق الإيقاف). |
| **delegate_application** | Flutter app for **collection delegates**. | تطبيق Flutter لـ **مندوبي الجباية**. |
| **delegate_application_aqeel** | Variant of the delegate Flutter app (Aqeel branding/branch line). | نسخة من تطبيق المندوب (خط/علامة عقيل). |
| **follower_application** | Flutter app for **followers** (collection lists, payments, day rating). | تطبيق Flutter لـ **المتابعين** (قوائم جباية، دفعات، تقييم يومي). |
| **main_follower_application** | Flutter app for **main/committee follower** workflows. | تطبيق Flutter لمسار **المتابع الرئيسي/اللجان**. |
| **branch_manager_application** | Flutter app for **branch managers** (visits, local ops). | تطبيق Flutter لـ **مدراء الفروع** (زيارات، عمليات محلية). |
| **delegated_manager_application** | Flutter app for **delegated managers** (director-side mobile). | تطبيق Flutter لـ **المدير المفوض** (موبايل جهة المدير). |
| **mobile_app** | Shared/legacy Flutter shell or company mobile package in the monorepo. | حزمة/شل Flutter مشتركة أو قديمة للشركة داخل المونوريبو. |
| **MA_ShortCompany** | Mobile (Flutter) companion for the short company frontend line. | موبايل Flutter مرافق لخط الواجهة المختصرة. |

---

## What is intentionally NOT on `main` / ما غير موجود عمداً على `main`

| Excluded | Why EN | لماذا AR |
|----------|--------|----------|
| `deploy/`, `scripts/`, `tools/`, `_publish_*` | Ops/publish scratch — not needed to develop features | سكربتات تشغيل/نشر مؤقتة — غير لازمة لتطوير الميزات |
| `*.py`, `*.ps1`, `*.bat` | Local automation & probes | أتمتة وفحص محلي |
| `.cursor/`, `.agents/`, skills | Editor/agent tooling | أدوات المحرر/الوكيل |
| `appsettings.json`, `.env`, keystores | Secrets — use examples/templates only | أسرار — استخدم الأمثلة/القوالب فقط |

See root `.gitignore` for the full exclude list.

---

## Suggested reading order for a new programmer / ترتيب قراءة مقترح

1. This file — `docs/REPO_FOLDERS_SHEET_AR_EN.md`
2. Excel APIs — `docs/api-excel-documentation.md`
3. Sales requests evaluation design — `docs/superpowers/specs/2026-09-13-sales-manager-requests-evaluation-design.md`
4. Shared FE contracts — `docs/shared-fe-contracts.md`
5. Database index — `docs/databasescripts-index.md`

---

## Runtime mental model / نموذج التشغيل باختصار

```text
Flutter / FE_Company
        │
        ▼
 BE_SalesEmployee  (Sales Gateway — multi-branch)
        │
        ▼
 BE_Company per Admin* branch DB
        │
 BE_DelegateWebApplication  ◄── delegate/follower mobiles (field)
```

| EN | AR |
|----|----|
| Gateway merges cross-province reads (Excel search, evaluations). | البوابة تدمج قراءات عبر المحافظات (بحث إكسل، تقييمات). |
| Each `Admin*` folder on the server is one BE_Company deployment + DB. | كل مجلد `Admin*` على السيرفر نشر BE_Company + قاعدة فرع. |
