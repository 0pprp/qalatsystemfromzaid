"""Business-function enrichment, duplicate classification, and call-chain inference."""

from __future__ import annotations

import re
from collections import defaultdict

POLICY_ROLE_AR = {
    "موظف مبيعات": "موظف مبيعات",
    "مدير مبيعات": "مدير مبيعات",
}

# Ordered patterns: first match wins. (method or *, route substring/regex, function, domain, ar, en)
ROUTE_FUNCTIONS = [
    ("POST", r"^/api/sales-manager/exceptions$", "Create Sales Exception", "Sales Exceptions", "إنشاء طلب استثناء مبيعات", "Create a pending sales-exception request"),
    ("GET", r"^/api/sales-manager/exceptions$", "List Sales Exceptions", "Sales Exceptions", "عرض طلبات الاستثناء", "List sales-exception requests for the caller"),
    ("GET", r"^/api/sales-manager/exceptions/\{", "Get Sales Exception Details", "Sales Exceptions", "تفاصيل طلب استثناء", "Get one sales-exception request"),
    ("POST", r"/exceptions/.*/consume", "Assign Approved Exception", "Sales Exceptions", "تعيين الاستثناء المعتمد لموظف", "Consume/assign an approved exception to an employee"),
    ("POST", r"/exceptions/.*/cancel", "Cancel Sales Exception", "Sales Exceptions", "إلغاء طلب استثناء", "Cancel a pending sales exception"),
    ("PUT", r"/exceptions/.*/decision", "Approve Or Reject Sales Exception", "Sales Exceptions", "اعتماد أو رفض الاستثناء", "Delegated manager decision on an exception"),
    ("POST", r"^/api/internal/sales-exceptions$", "Create Sales Exception", "Sales Exceptions", "إنشاء طلب استثناء (داخلي)", "Trusted branch intake for sales exceptions"),
    ("GET", r"^/api/internal/sales-exceptions$", "List Sales Exceptions", "Sales Exceptions", "عرض طلبات الاستثناء (داخلي)", "Trusted list of sales exceptions"),
    ("GET", r"^/api/internal/sales-exceptions/\{", "Get Sales Exception Details", "Sales Exceptions", "تفاصيل استثناء (داخلي)", "Trusted get of one sales exception"),
    ("POST", r"^/api/internal/sales-exceptions/\{id\}/consume$", "Assign Approved Exception", "Sales Exceptions", "تعيين الاستثناء المعتمد (داخلي)", "Trusted consume of an approved exception"),
    ("GET", r"^/api/delegated-manager/exceptions$", "List Sales Exceptions", "Sales Exceptions", "عرض الاستثناءات للمدير المفوض", "Delegated-manager inbox of exceptions"),
    ("GET", r"^/api/delegated-manager/exceptions/\{", "Get Sales Exception Details", "Sales Exceptions", "تفاصيل استثناء للمدير المفوض", "Delegated-manager exception detail"),
    ("PUT", r"^/api/delegated-manager/exceptions/\{id\}/decision$", "Approve Or Reject Sales Exception", "Sales Exceptions", "قرار المدير المفوض على الاستثناء", "Approve or reject from delegated-manager app"),
    ("POST", r"^/api/delegate/complaints$", "Create Complaint", "Complaints", "إنشاء شكوى مندوب", "Delegate files a complaint; branch forwards to central inbox"),
    ("POST", r"^/api/internal/complaints$", "Create Complaint", "Complaints", "إنشاء شكوى (داخلي)", "Trusted branch intake into CentralComplaints"),
    ("POST", r"^/api/complaints$", "Create Complaint", "Complaints", "إنشاء شكوى عبر البوابة", "Authenticated gateway user files a central complaint"),
    ("GET", r"^/api/delegated-manager/complaints$", "List Complaints", "Complaints", "عرض الشكاوى", "Delegated-manager complaint inbox"),
    ("GET", r"^/api/delegated-manager/complaints/unread-count$", "Get Unread Complaint Count", "Complaints", "عدد الشكاوى غير المقروءة", "Unread complaint badge"),
    ("GET", r"^/api/delegated-manager/complaints/\{", "Get Complaint Details", "Complaints", "تفاصيل شكوى", "One central complaint"),
    ("PUT", r"^/api/delegated-manager/complaints/\{id\}/read$", "Mark Complaint Read", "Complaints", "تعليم الشكوى مقروءة", "Mark one complaint read"),
    ("PUT", r"^/api/delegated-manager/complaints/read-all$", "Mark All Complaints Read", "Complaints", "تعليم كل الشكاوى مقروءة", "Mark all complaints read"),
    ("GET", r"^/api/delegated-manager/dashboard$", "Get Delegated Manager Dashboard", "Delegated Manager", "لوحة المدير المفوض", "Delegated-manager dashboard"),
    ("GET", r"^/api/mobile-updates/\{", "Get Mobile Update Metadata", "Mobile Updates", "بيانات تحديث التطبيق", "Anonymous APK update metadata by appKey"),
    ("POST", r"^/api/Auth/LoginSalesManager$", "Login Sales Manager", "Auth", "دخول مدير المبيعات المركزي", "Central sales-manager login on the gateway"),
    ("POST", r"^/api/Auth/LoginDelegatedManager$", "Login Delegated Manager", "Auth", "دخول المدير المفوض", "Central delegated-manager login"),
    ("POST", r"^/api/Auth/LoginSalesFilter$", "Login Sales Filter", "Auth", "دخول موظف الفلترة", "Sales-filter employee login on the gateway"),
    ("POST", r"^/api/Auth/Login$", "Login Sales Employee", "Auth", "دخول موظف المبيعات عبر البوابة", "Gateway sales-employee login, proxies branch Users_LoginEmployee"),
    ("GET", r"^/api/Auth/Cities$", "List Login Cities", "Auth", "قائمة محافظات الدخول", "City catalog for login"),
    ("GET", r"^/api/Auth/me$", "Get Current Gateway User", "Auth", "المستخدم الحالي للبوابة", "Gateway token identity"),
    ("POST", r"^/api/Users/Users_LoginAdmin$", "Login Admin", "Auth", "دخول الأدمن / مدير النظام", "Branch admin login"),
    ("POST", r"^/api/Users/Users_LoginEmployee$", "Login Employee", "Auth", "دخول الموظف", "Branch employee login (sales, filter, office)"),
    ("POST", r"^/api/Users/Users_RefreshToken$", "Refresh Token", "Auth", "تجديد التوكن", "Refresh JWT"),
    ("GET", r"/customers/search", "Search Customer", "Customers", "بحث عن زبون", "Search customers"),
    ("GET", r"/SalesEmployee/SearchCustomers/", "Search Customer", "Customers", "بحث عن زبون (قديم)", "Legacy sales-employee customer search"),
    ("GET", r"/customers/directory", "List Customer Directory", "Customers", "دليل الزبائن", "Anonymous/directory listing of customers"),
    ("POST", r"/customers/excel-search", "Search Customers From Excel", "Customers", "بحث زبائن من إكسل", "Excel-driven customer search"),
    ("POST", r"/api/sales-manager/sales-requests$", "Create Sales Request", "Sales Requests", "إنشاء طلب بيع من مدير المبيعات", "Sales manager creates a sales request"),
    ("POST", r"/api/sales/requests$", "Create Sales Request", "Sales Requests", "إنشاء طلب بيع من موظف المبيعات", "Sales employee creates a sales request"),
    ("POST", r"/api/sales/requests/submit$", "Create Sales Request", "Sales Requests", "إرسال طلب بيع", "Submit sales request (alias/legacy path)"),
    ("GET", r"/sales-requests$", "List Sales Requests", "Sales Requests", "عرض طلبات البيع", "List sales requests"),
    ("GET", r"/sales/requests$", "List Sales Requests", "Sales Requests", "عرض طلبات البيع للموظف", "Employee list of assigned/created requests"),
    ("POST", r"/sales-requests/.*/assign$", "Assign Sales Request", "Sales Requests", "تعيين طلب بيع لموظف", "Assign a sales request to an employee"),
    ("POST", r"/sales-requests/.*/reassign$", "Reassign Sales Request", "Sales Requests", "إعادة تعيين طلب بيع", "Reassign a sales request"),
    ("POST", r"/sales-requests/.*/reject$", "Reject Sales Request", "Sales Requests", "رفض طلب بيع", "Reject a sales request"),
    ("POST", r"/sales/requests/.*/reject$", "Reject Sales Request", "Sales Requests", "رفض طلب بيع من الموظف", "Employee reject of a request"),
    ("POST", r"/sales-requests/.*/prepare$", "Prepare Sales Request", "Sales Requests", "تجهيز طلب بيع", "Mark request prepared"),
    ("POST", r"/sales/requests/.*/prepare$", "Prepare Sales Request", "Sales Requests", "تجهيز طلب بيع", "Employee prepare"),
    ("POST", r"/sales-requests/.*/pending$", "Set Sales Request Pending", "Sales Requests", "تحويل الطلب إلى معلّق", "Set request pending"),
    ("POST", r"/sales/requests/.*/pending$", "Set Sales Request Pending", "Sales Requests", "تحويل الطلب إلى معلّق", "Employee pending"),
    ("POST", r"/sales-requests/.*/return$", "Return Sales Request", "Sales Requests", "إرجاع طلب بيع", "Return request to queue"),
    ("POST", r"/sales-requests/import$", "Upload Sales Excel", "Sales Requests", "استيراد طلبات بيع من إكسل", "Import sales requests from Excel"),
    ("POST", r"/sales-requests/.*/exception-hold$", "Hold Sales Request For Exception", "Sales Requests", "تعليق الطلب بانتظار الاستثناء", "Hold a request while an exception is pending"),
    ("PUT", r"/sales-requests/.*/exception-hold$", "Hold Sales Request For Exception", "Sales Requests", "تعليق الطلب بانتظار الاستثناء", "Hold a request while an exception is pending"),
    ("POST", r"/sales-requests/evaluate$", "Evaluate Sales Request Name", "Sales Requests", "تقييم تشابه اسم الزبون", "Name-evaluation against existing customers"),
    ("POST", r"/sales-requests/.*/evaluate$", "Evaluate Sales Request Name", "Sales Requests", "تقييم تشابه اسم الزبون", "City-addressed name evaluation on gateway"),
    ("POST", r"/sales-filter/requests/.*/hold$", "Hold Sales Request Filter", "Sales Filter", "تعليق طلب من موظف الفلترة", "Filter employee holds a request"),
    ("POST", r"/sales-filter/requests/.*/ready$", "Mark Sales Request Ready", "Sales Filter", "تعليم الطلب جاهزاً", "Filter employee marks ready"),
    ("POST", r"/sales-filter/requests/.*/reject$", "Reject Sales Request Filter", "Sales Filter", "رفض طلب من موظف الفلترة", "Filter employee rejects"),
    ("GET", r"/sales-filter/requests$", "List Filter Sales Requests", "Sales Filter", "طلبات الفلترة", "Filter queue"),
    ("GET", r"/sales-filter/counts$", "Get Filter Queue Counts", "Sales Filter", "عدادات طابور الفلترة", "Filter queue counts"),
    ("GET", r"/sales-filter/sales-employees$", "List Filter Sales Employees", "Sales Filter", "موظفون للفلترة", "Employees visible to filter role"),
    ("GET", r"/sales-filter/me/cities$", "List Filter Cities", "Sales Filter", "محافظات موظف الفلترة", "Cities assigned to the filter user"),
    ("POST", r"/sales/shifts/start$", "Start Shift", "Shift / GPS", "بدء الوردية", "Start sales-employee shift"),
    ("POST", r"/SalesEmployee/ShiftStart$", "Start Shift", "Shift / GPS", "بدء الوردية (قديم)", "Legacy start-shift"),
    ("POST", r"/api/Shift/Start$", "Start Shift", "Shift / GPS", "بدء الوردية عبر البوابة القديمة", "Gateway alias for ShiftStart"),
    ("GET", r"/sales/shifts/current$", "Get Current Shift", "Shift / GPS", "الوردية الحالية", "Current shift"),
    ("GET", r"/SalesEmployee/ShiftStatus$", "Get Current Shift", "Shift / GPS", "حالة الوردية (قديم)", "Legacy shift status"),
    ("GET", r"/api/Shift/Status$", "Get Current Shift", "Shift / GPS", "حالة الوردية عبر البوابة القديمة", "Gateway alias for ShiftStatus"),
    ("POST", r"/sales/shifts/end$", "End Shift", "Shift / GPS", "إنهاء الوردية", "End shift"),
    ("POST", r"/sales/location/batch$", "Submit Location Batch", "Shift / GPS", "إرسال نقاط GPS دفعة", "Batch GPS points"),
    ("POST", r"/SalesEmployee/TrackSync$", "Submit Location Batch", "Shift / GPS", "مزامنة التتبع (قديم)", "Legacy track sync"),
    ("POST", r"/api/Track/Sync$", "Submit Location Batch", "Shift / GPS", "مزامنة التتبع عبر البوابة القديمة", "Gateway alias for TrackSync"),
    ("POST", r"/sales/location/live$", "Submit Live Location", "Shift / GPS", "موقع حي", "Live location ping"),
    ("POST", r"/SalesEmployee/Ratings$", "Rate Customer", "Ratings", "تقييم زبون من موظف المبيعات", "Save a customer rating from the sales employee"),
    ("POST", r"^/api/Ratings$", "Rate Customer", "Ratings", "تقييم زبون عبر البوابة القديمة", "Gateway alias posting to SalesEmployee/Ratings"),
    ("POST", r"/api/sales/Create$", "Create Sale Legacy", "Sales", "إنشاء مبيع عبر المسار القديم", "Gateway POST /api/sales/Create proxies CustomersSales_Create"),
    ("POST", r"^/api/sales$", "Create Sale In Progress", "Sales", "إنشاء مبيع قيد التنفيذ", "Create in-progress sale"),
    ("POST", r"/sales/progress$", "Create Sale In Progress", "Sales", "حفظ تقدم المبيع", "Save sale progress"),
    ("POST", r"/sales/.*/complete$", "Complete Sale", "Sales", "إكمال المبيع", "Complete a sale"),
    ("GET", r"^/api/sales/me$", "Get Sales Employee Profile", "Sales", "ملف موظف المبيعات", "Current sales-employee profile"),
    ("GET", r"^/api/sales/today$", "Get Today Sales", "Sales", "مبيعات اليوم", "Today sales"),
    ("GET", r"^/api/sales/pending$", "Get Pending Sales", "Sales", "مبيعات معلّقة", "Pending sales"),
    ("GET", r"^/api/sales/inventory$", "List Sales Inventory", "Sales", "جرد/مواد المبيعات", "Inventory for sales employee"),
    ("POST", r"/CustomersSales/CustomersSales_Create$", "Create Customer Sale", "Sales", "إنشاء مبيع زبون", "ERP create customer sale"),
    ("POST", r"/sales-requests/.*/mark-read$", "Mark Sales Request Read", "Sales Requests", "تعليم الطلب مقروءاً", "Mark one request read"),
    ("POST", r"/sales-requests/mark-all-read$", "Mark All Sales Requests Read", "Sales Requests", "تعليم كل الطلبات مقروءة", "Mark all requests read"),
    ("GET", r"/sales-requests/unread-count$", "Get Unread Sales Request Count", "Sales Requests", "عدد الطلبات غير المقروءة", "Unread request badge"),
    ("GET", r"/hubs/sales-tracking$", "Sales Tracking Hub", "Shift / GPS", "بث مواقع الموظفين لحظياً", "SignalR hub for live employee locations"),
]

ACTION_PREFIX_AR = {
    "Get": "جلب",
    "List": "عرض",
    "Create": "إنشاء",
    "Update": "تحديث",
    "Delete": "حذف",
    "Login": "دخول",
    "Search": "بحث",
    "Assign": "تعيين",
}

DB_BY_CONTROLLER = {
    "BE_Company::SalesManagerExceptionsController": ("Sales Gateway via forwarder", "SalesExceptionService → SalesExceptionRequests"),
    "BE_Company::SalesManagerController": ("Branch company DB (Sales schema)", "ISalesRequestService / sales manager services"),
    "BE_Company::SalesController": ("Branch company DB (Sales schema)", "Sales request/sale/shift services"),
    "BE_Company::SalesFilterController": ("Branch company DB (Sales schema)", "ISalesFilterService"),
    "BE_Company::CustomersController": ("Branch company DB", "ICustomersRepository"),
    "BE_Company::CustomersSalesController": ("Branch company DB", "ICustomersSalesRepository / CustomersSales"),
    "BE_Company::CustomersPaymentsController": ("Branch company DB", "ICustomersPaymentsRepository / CustomerPayment"),
    "BE_Company::CustomersPaymentsRequestController": ("Branch company DB", "CustomersPaymentsRequests"),
    "BE_Company::DelegatesController": ("Branch company DB", "Delegates"),
    "BE_Company::UsersController": ("Branch company DB", "IUsersRepository / Users"),
    "BE_Company::PortalController": ("Branch company DB", "Customers + Payments + Sales via encrypted token"),
    "BE_Company::SalesEmployeeController": ("Branch company DB", "ISalesEmployeeRepository / shifts, ratings, search"),
    "BE_Company::RatingsController": ("Branch company DB", "Ratings"),
    "BE_Company::CustomerDecisionsController": ("Branch company DB", "CustomerDecisions / Notes"),
    "BE_Company::CustomersNotesController": ("Branch company DB", "ISharedCustomerNotesService"),
    "BE_Company::FollowerUserListsController": ("Branch company DB", "Follower user lists"),
    "BE_Company::FollowerTrackingAccountantController": ("Delegate DB via HTTP or shared follower tracking", "Follower GPS for accountant"),
    "BE_SalesEmployee::InternalSalesExceptionsController": ("Central Sales Gateway DB", "SalesExceptionService / SalesExceptionRequests"),
    "BE_SalesEmployee::SalesManagerExceptionsController": ("Central Sales Gateway DB", "SalesExceptionService / SalesExceptionRequests"),
    "BE_SalesEmployee::DelegatedManagerController": ("Central Sales Gateway DB", "CentralComplaints + SalesExceptionService"),
    "BE_SalesEmployee::InternalComplaintsController": ("Central Sales Gateway DB", "CentralComplaintsService / CentralComplaints"),
    "BE_SalesEmployee::ComplaintsController": ("Central Sales Gateway DB", "CentralComplaintsService / CentralComplaints"),
    "BE_SalesEmployee::MobileUpdatesController": ("Central Sales Gateway DB", "MobileUpdateService / mobile update store"),
    "BE_SalesEmployee::AuthController": ("Gateway config + branch Users via proxy", "TokenService / BranchProxyService"),
    "BE_SalesEmployee::SalesManagerController": ("Branch company DB via proxy", "BranchProxyService → BE_Company sales-manager"),
    "BE_SalesEmployee::SalesController": ("Branch company DB via proxy", "BranchProxyService → BE_Company /api/sales"),
    "BE_SalesEmployee::SalesFilterController": ("Branch company DB via proxy", "BranchProxyService → BE_Company sales-filter"),
    "BE_SalesEmployee::SearchController": ("Branch company DBs via aggregator", "IGlobalCustomerSearchService"),
    "BE_SalesEmployee::ShiftController": ("Branch company DB via proxy", "SalesEmployee/Shift*"),
    "BE_SalesEmployee::TrackController": ("Branch company DB via proxy", "SalesEmployee/TrackSync"),
    "BE_SalesEmployee::RatingsController": ("Branch company DB via proxy", "SalesEmployee/Ratings"),
    "BE_SalesEmployee::StoresController": ("Branch company DB via proxy", "Stores/Items/Delegates"),
    "BE_DelegateWebApplication::DelegateComplaintsController": ("Delegate branch DB + CentralComplaints via forwarder", "IDelegateComplaintsService → CentralComplaintForwarder"),
    "BE_DelegateWebApplication::CustomersController": ("Delegate branch DB", "ICustomersRepository"),
    "BE_DelegateWebApplication::CustomersPaymentsController": ("Delegate branch DB", "ICustomersPaymentsRepository"),
    "BE_DelegateWebApplication::CustomersPaymentsRequestsController": ("Delegate branch DB", "ICustomersPaymentsRequestsRepository"),
    "BE_DelegateWebApplication::CustomersSalesController": ("Delegate branch DB", "ICustomersSalesRepository"),
    "BE_DelegateWebApplication::DelegatesController": ("Delegate branch DB", "IDelegateRepository"),
    "BE_DelegateWebApplication::FollowersController": ("Delegate branch DB", "Followers"),
    "BE_DelegateWebApplication::FollowerTrackingController": ("Delegate branch DB", "IFollowerTrackingService"),
    "BE_DelegateWebApplication::CustomersNotesController": ("Delegate branch DB", "ISharedCustomerNotesService"),
    "BE_DelegateWebApplication::TrustReceiptsController": ("Delegate branch DB", "ITrustReceiptRepository"),
}

GROUP_ID_SLUGS = {}

TRUE_DUPLICATE_HINTS = {
    "Start Shift",
    "Get Current Shift",
    "Submit Location Batch",
    "Rate Customer",
    "Create Sale Legacy",
    "Create Sales Request",
    "Search Customer",
    "Evaluate Sales Request Name",
}

BRIDGE_FUNCTIONS = {
    "Create Sales Exception",
    "List Sales Exceptions",
    "Get Sales Exception Details",
    "Assign Approved Exception",
    "Create Complaint",
    "List Sales Requests",
    "Assign Sales Request",
    "Upload Sales Excel",
    "Hold Sales Request For Exception",
}


def humanize_action(action: str) -> str:
    name = re.sub(r"([a-z])([A-Z])", r"\1 \2", action)
    name = name.replace("_", " ")
    name = re.sub(r"\s+", " ", name).strip()
    replacements = [
        ("Get All", "List"),
        ("GetBy", "Get by"),
        ("Get By", "Get by"),
    ]
    for a, b in replacements:
        name = name.replace(a, b)
    return name


def infer_function(ep) -> tuple[str, str, str, str]:
    route = ep.normalized_route
    method = ep.http_method
    for m, pattern, fn, domain, ar, en in ROUTE_FUNCTIONS:
        if m not in {"*", method}:
            continue
        if re.search(pattern, route, re.IGNORECASE):
            return fn, domain, ar, en
    # ERP-style action names
    domain = ep.controller.replace("Controller", "")
    fn = humanize_action(ep.action)
    ar = ""
    en = ep.xml_summary or f"{method} {route} → {ep.action}"
    rl = route.lower()
    if fn.lower() in {"get", "create", "update", "delete", "get by id", "live", "route", "followers", "list"} or fn == ep.action:
        pretty_domain = domain
        if pretty_domain.endswith("Controller"):
            pretty_domain = pretty_domain.replace("Controller", "")
        fn = f"{humanize_action(ep.action)} ({pretty_domain})".strip()
    if "/customerspayments" in rl.replace("_", ""):
        domain = "Customer Payments"
    elif "/customerssales" in rl.replace("_", ""):
        domain = "Customer Sales"
    elif "/customers" in rl:
        domain = "Customers"
    elif "/delegates" in rl:
        domain = "Delegates"
    elif "/followers" in rl:
        domain = "Followers"
    elif "/users" in rl:
        domain = "Users"
    elif "/items" in rl:
        domain = "Items"
    elif "/stores" in rl:
        domain = "Stores"
    elif "/accounts" in rl:
        domain = "Accounts"
    elif "/buys" in rl:
        domain = "Purchases"
    elif "/employees" in rl:
        domain = "Employees"
    elif "/portal" in rl:
        domain = "Customer Portal"
    elif "/backup" in rl:
        domain = "Backup"
    return fn, domain, ar, en


def slug_group(fn: str) -> str:
    if fn in GROUP_ID_SLUGS:
        return GROUP_ID_SLUGS[fn]
    slug = re.sub(r"[^A-Za-z0-9]+", "_", fn.upper()).strip("_")
    GROUP_ID_SLUGS[fn] = slug
    return slug


def db_for(ep) -> tuple[str, str]:
    key = f"{ep.service}::{ep.controller}"
    if key in DB_BY_CONTROLLER:
        return DB_BY_CONTROLLER[key]
    if ep.injected_types:
        return ("See injected service", ", ".join(ep.injected_types[:4]))
    return ("Unknown / not inferred", "")


def _role_family(route: str, service: str) -> str:
    r = route.lower()
    if "/internal/" in r:
        return "internal"
    if "delegated-manager" in r:
        return "delegated-manager"
    if "sales-filter" in r:
        return "sales-filter"
    if "sales-manager" in r:
        return "sales-manager"
    if "/api/sales/" in r or r.rstrip("/").endswith("/api/sales") or r == "/api/sales":
        return "sales-employee"
    if any(tok in r for tok in ("/api/shift", "/api/track", "/salesemployee/", "/api/ratings")):
        return "sales-legacy"
    if "follower" in r:
        return "follower"
    if "delegate" in r or service == "BE_DelegateWebApplication":
        return "delegate"
    if service == "BE_SalesEmployee":
        return "gateway-other"
    if service == "BE_Company":
        return "company-erp"
    return service


def classify_group(fn: str, members: list[dict]) -> tuple[str, int, str, str]:
    """Return classification, risk, why, canonical."""
    n = len(members)
    services = {m["service"] for m in members}
    internals = [m for m in members if "/internal/" in m["normalizedRoute"].lower()]
    publics = [m for m in members if m not in internals]
    families = {_role_family(m["normalizedRoute"], m["service"]) for m in publics}

    def canonical_of(cands):
        ranked = sorted(
            cands,
            key=lambda m: (
                0 if "/internal/" not in m["normalizedRoute"] else 1,
                0 if m["service"] == "BE_SalesEmployee" and "{cityvalue}" not in m["normalizedRoute"].lower() else 1,
                len(m["normalizedRoute"]),
            ),
        )
        return f"{ranked[0]['method']} {ranked[0]['normalizedRoute']}" if ranked else members[0]["normalizedRoute"]

    if n == 1:
        return "A. UNIQUE", 0, "Only one implementation found.", members[0]["normalizedRoute"]

    routes = {m["normalizedRoute"] for m in members}
    if len(routes) == 1 and len({m["service"] for m in members}) == 1:
        return (
            "A. UNIQUE",
            0,
            "Single implementation advertised with more than one HTTP verb on the same route.",
            f"{members[0]['method']} {members[0]['normalizedRoute']}",
        )

    legacy_tokens = ("/api/shift", "/api/track", "/salesemployee/", "/api/sales/create")
    has_legacy = any(any(tok in m["normalizedRoute"].lower() for tok in legacy_tokens) for m in members)
    has_modern = any("/api/sales/" in m["normalizedRoute"].lower() for m in members)

    city_path = [
        m
        for m in members
        if "{cityvalue}" in m["normalizedRoute"].lower() or "{cityValue}" in m["normalizedRoute"]
    ]
    same_service_city = len({m["service"] for m in city_path}) == 1 and city_path
    non_city_same = [
        m
        for m in members
        if m["service"] in {c["service"] for c in city_path}
        and m not in city_path
        and _role_family(m["normalizedRoute"], m["service"]) in {_role_family(c["normalizedRoute"], c["service"]) for c in city_path}
    ]

    # Same gateway service, city-in-path alias of an existing route.
    if same_service_city and non_city_same and len(families - {"internal"}) <= 2:
        why = (
            "Same service exposes both a city-in-path variant and a query/body variant for the same operation. "
            "Prefer one shape per trust boundary (usually ?cityValue= on lists, path city only when required)."
        )
        return "E. TRUE DUPLICATE", 4, why, canonical_of(non_city_same)

    public_families = families - {"internal"}
    if len(public_families) > 1 and not (public_families <= {"sales-employee", "sales-legacy"}):
        why = (
            "Same general capability with different authorization or caller roles "
            f"({', '.join(sorted(public_families))}). Keep separate public entry points per role. "
        )
        if internals:
            why += "An /api/internal/* intake in this group is a legitimate bridge, not a third client API."
        return "C. ROLE-SPECIFIC", 1, why, canonical_of(publics)

    if has_legacy and has_modern:
        modern = next(m for m in members if "/api/sales/" in m["normalizedRoute"].lower())
        why = (
            "Older gateway/branch aliases still proxy the same capability as modern /api/sales/* routes. "
            "Do not delete until every mobile build stops calling the alias."
        )
        return "D. LEGACY", 3, why, f"{modern['method']} {modern['normalizedRoute']}"

    if internals and publics:
        why = (
            "Multiple endpoints exist across trust boundaries: a client/branch entry point "
            "and a trusted internal gateway intake. This is architectural, not accidental duplication."
        )
        return "B. LEGITIMATE BRIDGE", 2, why, canonical_of(publics)

    branches = [m for m in publics if m["service"] == "BE_Company"]
    gateways = [m for m in publics if m["service"] == "BE_SalesEmployee"]
    if branches and gateways and len(public_families) <= 1:
        why = (
            "BE_SalesEmployee exposes a central gateway route that proxies the BE_Company branch route. "
            "Clients talk to one trust boundary; the second endpoint is the branch implementation."
        )
        return "B. LEGITIMATE BRIDGE", 2, why, canonical_of(gateways or publics)

    if len(services) == 1 and len(routes) > 1:
        why = (
            "More than one route on the same service implements the same business function. "
            "Likely an alias or copy on the same trust boundary."
        )
        risk = 5 if n >= 3 else 4
        return "E. TRUE DUPLICATE", risk, why, canonical_of(members)

    if len(public_families) > 1:
        return "C. ROLE-SPECIFIC", 1, "Different roles or apps share this capability.", canonical_of(publics)

    why = "Multiple endpoints share this function; evidence is insufficient to distinguish bridge vs accident."
    return "F. UNKNOWN / REVIEW REQUIRED", 3, why, canonical_of(members)


def status_for(ep, consumer_apps: list[str]) -> str:
    route = ep.normalized_route.lower()
    if ep.kind == "signalr":
        return "ACTIVE"
    if "/internal/" in route:
        return "ACTIVE-INTERNAL"
    if any(tok in route for tok in ("/api/shift", "/api/track", "/salesemployee/", "/api/sales/create")):
        if consumer_apps:
            return "LEGACY"
        return "LEGACY"
    prod_apps = [a for a in consumer_apps if not a.startswith("BE_")]
    if not prod_apps:
        return "ACTIVE"
    return "ACTIVE"


def enrich(endpoints, consumers, matches_by_endpoint_id: dict) -> tuple[list[dict], list[dict], list[dict]]:
    rows = []
    for i, ep in enumerate(endpoints, 1):
        fn, domain, ar, en = infer_function(ep)
        db, repo = db_for(ep)
        eid = f"EP-{i:04d}"
        downstream_service = ""
        downstream_ep = ""
        if ep.downstream_hints:
            downstream_ep = "; ".join(f"/api/{h}" if not h.startswith("api/") else "/" + h for h in ep.downstream_hints[:6])
            if ep.service == "BE_SalesEmployee":
                downstream_service = "BE_Company"
            elif ep.service == "BE_Company" and any("internal/" in h for h in ep.downstream_hints):
                downstream_service = "BE_SalesEmployee"
            elif ep.service == "BE_DelegateWebApplication" and any("internal/" in h for h in ep.downstream_hints):
                downstream_service = "BE_SalesEmployee"
        elif ep.service == "BE_Company" and ep.controller == "SalesManagerExceptionsController":
            downstream_service = "BE_SalesEmployee"
            downstream_ep = "/api/internal/sales-exceptions"
        elif ep.service == "BE_DelegateWebApplication" and ep.controller == "DelegateComplaintsController":
            downstream_service = "BE_SalesEmployee"
            downstream_ep = "/api/internal/complaints"

        rows.append(
            {
                "endpointId": eid,
                "service": ep.service,
                "project": ep.project,
                "controller": ep.controller,
                "action": ep.action,
                "method": ep.http_method,
                "route": ep.full_route,
                "normalizedRoute": ep.normalized_route,
                "businessDomain": domain,
                "businessFunction": fn,
                "arabicBusinessDescription": ar or en,
                "technicalDescription": en or ep.xml_summary,
                "authRequired": ep.auth_required,
                "authType": ep.auth_type,
                "allowedRoles": ep.allowed_roles,
                "internalKey": ep.internal_key,
                "branchCentralScope": ep.branch_central_scope,
                "calledByApps": [],
                "calledByFiles": [],
                "downstreamService": downstream_service,
                "downstreamEndpoint": downstream_ep,
                "repositoryService": repo or ", ".join(ep.injected_types[:5]),
                "databaseTable": db,
                "status": "ACTIVE",
                "duplicateGroupId": slug_group(fn),
                "businessFunctionEndpointCount": 1,
                "duplicateClassification": "A. UNIQUE",
                "duplicateRiskScore": 0,
                "potentialReplacement": "",
                "notes": ep.notes or ep.xml_summary,
                "sourceFile": ep.source_file,
                "sourceLine": ep.source_line,
                "kind": ep.kind,
                "staticConsumerFound": False,
            }
        )

    by_id = {r["endpointId"]: r for r in rows}
    # attach consumers
    for ep, row in zip(endpoints, rows):
        recs = matches_by_endpoint_id.get(id(ep), [])
        apps = []
        files = []
        for c in recs:
            if c.app not in apps:
                apps.append(c.app)
            loc = f"{c.source_file}:{c.source_line}"
            if c.function_name:
                loc += f" ({c.function_name})"
            if loc not in files:
                files.append(loc)
        row["calledByApps"] = apps
        row["calledByFiles"] = files[:12]
        row["staticConsumerFound"] = any(a not in {"BE_Company", "BE_SalesEmployee", "BE_DelegateWebApplication"} for a in apps) or bool(apps)
        row["status"] = status_for(ep, apps)
        if not any(not a.startswith("BE_") for a in apps):
            row["notes"] = (row["notes"] + " | " if row["notes"] else "") + "NO STATIC CONSUMER FOUND"
        if "/internal/" in row["normalizedRoute"].lower():
            row["status"] = "ACTIVE-INTERNAL"

    groups = defaultdict(list)
    for row in rows:
        groups[row["businessFunction"]].append(row)

    group_meta = []
    for fn, members in groups.items():
        classification, risk, why, canonical = classify_group(fn, members)
        gid = slug_group(fn)
        count = len(members)
        for m in members:
            m["duplicateGroupId"] = gid
            m["businessFunctionEndpointCount"] = count
            m["duplicateClassification"] = classification
            m["duplicateRiskScore"] = risk
            m["potentialReplacement"] = canonical if m["normalizedRoute"] != canonical else ""
        group_meta.append(
            {
                "duplicateGroupId": gid,
                "businessFunction": fn,
                "endpointCount": count,
                "endpoints": [f"{m['method']} {m['normalizedRoute']} ({m['service']})" for m in members],
                "services": sorted({m["service"] for m in members}),
                "consumers": sorted({a for m in members for a in m["calledByApps"] if not a.startswith("BE_")}),
                "classification": classification,
                "riskScore": risk,
                "whyMultipleExist": why if count > 1 else "Single implementation.",
                "recommendedCanonicalEndpoint": canonical,
                "canConsolidate": classification.startswith("E.") or classification.startswith("D."),
                "recommendedAction": _action_for(classification, count),
                "migrationRisk": _migration(classification),
            }
        )
    group_meta.sort(key=lambda g: (-g["riskScore"], -g["endpointCount"], g["businessFunction"]))
    return rows, group_meta, _call_chains(rows)


def _action_for(classification: str, count: int) -> str:
    if classification.startswith("A."):
        return "Keep. Document as canonical."
    if classification.startswith("B."):
        return "Keep both sides of the trust boundary. Do not add a third public route."
    if classification.startswith("C."):
        return "Keep role-specific entry points. Do not merge across roles."
    if classification.startswith("D."):
        return "Prefer the modern route for new clients. Retire alias only after consumers migrate."
    if classification.startswith("E."):
        return "Choose one canonical route per trust boundary. Stop adding aliases."
    return "Review with owner before changing clients."


def _migration(classification: str) -> str:
    if classification.startswith("A.") or classification.startswith("B.") or classification.startswith("C."):
        return "Low"
    if classification.startswith("D."):
        return "Medium — mobile builds may still call aliases"
    if classification.startswith("E."):
        return "High if clients already split across routes"
    return "Unknown"


def _call_chains(rows: list[dict]) -> list[dict]:
    chains = []

    def find(service, method, contains) -> dict | None:
        for r in rows:
            if r["service"] == service and r["method"] == method and contains.lower() in r["normalizedRoute"].lower():
                return r
        return None

    specs = [
        {
            "feature": "Create Sales Exception",
            "client": "FE_Company",
            "clientCall": "POST /api/sales-manager/exceptions",
            "branch": "BE_Company SalesManagerExceptionsController",
            "internalCall": "POST /api/internal/sales-exceptions",
            "gateway": "BE_SalesEmployee",
            "service": "SalesExceptionService",
            "table": "SalesExceptionRequests",
            "notes": "FE never sends X-Sales-Gateway-Key. Branch JWT → forwarder → internal key.",
        },
        {
            "feature": "Create Sales Exception (central manager on gateway)",
            "client": "FE_Company (central)",
            "clientCall": "POST /api/sales-manager/exceptions",
            "branch": "n/a (gateway JWT)",
            "internalCall": "n/a",
            "gateway": "BE_SalesEmployee SalesManagerExceptionsController",
            "service": "SalesExceptionService",
            "table": "SalesExceptionRequests",
            "notes": "Same store as the internal intake. Legitimate second public entry at gateway trust boundary.",
        },
        {
            "feature": "Approve Or Reject Sales Exception",
            "client": "delegated_manager_application",
            "clientCall": "PUT /api/delegated-manager/exceptions/{id}/decision",
            "branch": "n/a",
            "internalCall": "n/a",
            "gateway": "BE_SalesEmployee DelegatedManagerController",
            "service": "SalesExceptionService",
            "table": "SalesExceptionRequests",
            "notes": "Decision is central-only. Branch managers cannot approve.",
        },
        {
            "feature": "Assign Approved Exception",
            "client": "FE_Company",
            "clientCall": "POST /api/sales-manager/exceptions/{id}/consume",
            "branch": "BE_Company",
            "internalCall": "POST /api/internal/sales-exceptions/{id}/consume",
            "gateway": "BE_SalesEmployee",
            "service": "SalesExceptionService",
            "table": "SalesExceptionRequests",
            "notes": "After delegated-manager approval, sales manager assigns an employee.",
        },
        {
            "feature": "Delegate Complaint",
            "client": "delegate_application",
            "clientCall": "POST /api/delegate/complaints",
            "branch": "BE_DelegateWebApplication DelegateComplaintsController",
            "internalCall": "POST /api/internal/complaints",
            "gateway": "BE_SalesEmployee",
            "service": "CentralComplaintsService",
            "table": "CentralComplaints (+ local delegate audit row)",
            "notes": "Auth is X-Async-Id. Mobile never calls the internal route.",
        },
        {
            "feature": "List Complaints",
            "client": "delegated_manager_application",
            "clientCall": "GET /api/delegated-manager/complaints",
            "branch": "n/a",
            "internalCall": "n/a",
            "gateway": "BE_SalesEmployee DelegatedManagerController",
            "service": "CentralComplaintsService",
            "table": "CentralComplaints",
            "notes": "Read side is central-only.",
        },
        {
            "feature": "Sales Employee Search Customer",
            "client": "sales_employee_application",
            "clientCall": "GET /api/sales/customers/search",
            "branch": "BE_Company SalesController",
            "internalCall": "GET /api/sales/customers/search (proxied)",
            "gateway": "BE_SalesEmployee SalesController",
            "service": "GatewayGlobalCustomerSearchService / branch sales search",
            "table": "Customers (branch DBs)",
            "notes": "Legacy GET /api/SalesEmployee/SearchCustomers still used as gateway-key fallback.",
        },
        {
            "feature": "Assign Sales Request",
            "client": "FE_Company",
            "clientCall": "POST /api/sales-manager/sales-requests/{id}/assign",
            "branch": "BE_Company SalesManagerController",
            "internalCall": "proxied same path without cityValue",
            "gateway": "BE_SalesEmployee SalesManagerController",
            "service": "ISalesRequestService",
            "table": "SalesRequests (branch)",
            "notes": "Central FE inserts {cityValue} into the gateway path.",
        },
        {
            "feature": "Upload Sales Excel",
            "client": "FE_Company",
            "clientCall": "POST /api/sales-manager/sales-requests/import",
            "branch": "BE_Company",
            "internalCall": "POST sales-manager/sales-requests/import",
            "gateway": "BE_SalesEmployee",
            "service": "Sales request import",
            "table": "SalesRequests (branch)",
            "notes": "Bridge. Do not add a third import URL.",
        },
        {
            "feature": "Login Sales Manager",
            "client": "FE_Company",
            "clientCall": "POST /api/Auth/LoginSalesManager",
            "branch": "n/a (central account)",
            "internalCall": "n/a",
            "gateway": "BE_SalesEmployee AuthController",
            "service": "SalesManagerAccountService + TokenService",
            "table": "Gateway config (not branch Users)",
            "notes": "Distinct from Users_LoginAdmin on BE_Company.",
        },
        {
            "feature": "Login Sales Employee",
            "client": "sales_employee_application",
            "clientCall": "POST /api/Users/Users_LoginEmployee or POST /api/Auth/Login",
            "branch": "BE_Company UsersController",
            "internalCall": "POST Users/Users_LoginEmployee",
            "gateway": "BE_SalesEmployee AuthController",
            "service": "BranchProxyService",
            "table": "Users (branch)",
            "notes": "Demo Flutter may hit BE_Company directly; production uses gateway.",
        },
        {
            "feature": "Login Delegated Manager",
            "client": "delegated_manager_application",
            "clientCall": "POST /api/Auth/LoginDelegatedManager",
            "branch": "n/a",
            "internalCall": "n/a",
            "gateway": "BE_SalesEmployee AuthController",
            "service": "DelegatedManagerAccountService",
            "table": "Gateway config",
            "notes": "Central account, all branches.",
        },
        {
            "feature": "Get Mobile Update Metadata",
            "client": "delegate_application, delegated_manager_application, sales apps",
            "clientCall": "GET /api/mobile-updates/{appKey}",
            "branch": "n/a",
            "internalCall": "n/a",
            "gateway": "BE_SalesEmployee MobileUpdatesController",
            "service": "MobileUpdateService",
            "table": "Mobile update store",
            "notes": "AllowAnonymous by design (pre-login).",
        },
        {
            "feature": "Start Shift",
            "client": "sales_employee_application",
            "clientCall": "POST /api/sales/shifts/start",
            "branch": "BE_Company SalesController",
            "internalCall": "POST sales/shifts/start",
            "gateway": "BE_SalesEmployee SalesController",
            "service": "Shift tracking",
            "table": "Sales employee shifts (branch)",
            "notes": "Legacy POST /api/Shift/Start and POST /api/SalesEmployee/ShiftStart still exist.",
        },
        {
            "feature": "Follower Tracking (accountant)",
            "client": "FE_Company",
            "clientCall": "GET /api/follower-tracking/*",
            "branch": "BE_Company FollowerTrackingAccountantController",
            "internalCall": "may call delegate follower APIs",
            "gateway": "n/a",
            "service": "Follower tracking",
            "table": "Follower GPS (delegate DB)",
            "notes": "Office accountant view of follower GPS.",
        },
        {
            "feature": "Customer Portal",
            "client": "CustomerPortal / shared link",
            "clientCall": "GET /api/Portal/CustomerInfo|Payments|Sales?token=",
            "branch": "BE_Company PortalController",
            "internalCall": "n/a",
            "gateway": "n/a",
            "service": "Customers repositories",
            "table": "Customers / CustomerPayment / CustomersSales",
            "notes": "No JWT; encrypted query token. CustomerPortal SPA in-repo has no static axios calls.",
        },
    ]
    return specs
