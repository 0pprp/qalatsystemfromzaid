"""Extract static API consumers from web, Flutter, and backend-to-backend code."""

from __future__ import annotations

import re
from dataclasses import dataclass
from pathlib import Path

from scan_csharp import normalize_route

SKIP_DIR_NAMES = {
    "bin",
    "obj",
    "node_modules",
    "dist",
    "build",
    ".dart_tool",
    "ios",
    "android",
    "windows",
    "linux",
    "macos",
    "coverage",
    "@core",
    "plugins",
    ".git",
}

APPS = {
    "FE_Company": {
        "kind": "web",
        "roots": ["FE_Company/src"],
        "exts": {".js", ".vue", ".ts"},
        "sheet": "02_FE_Company",
    },
    "FE_ShortCompany": {
        "kind": "web",
        "roots": ["FE_ShortCompany/src"],
        "exts": {".js", ".vue", ".ts"},
        "sheet": "08_FE_ShortCompany",
    },
    "CustomerPortal": {
        "kind": "web",
        "roots": ["CustomerPortal/src"],
        "exts": {".js", ".vue", ".ts"},
        "sheet": "09_CustomerPortal",
    },
    "delegate_application": {
        "kind": "mobile",
        "roots": ["delegate_application/lib"],
        "exts": {".dart"},
        "sheet": "03_Delegate_App",
    },
    "delegate_application_aqeel": {
        "kind": "mobile",
        "roots": ["delegate_application_aqeel/lib"],
        "exts": {".dart"},
        "sheet": "12_Delegate_Aqeel",
    },
    "delegated_manager_application": {
        "kind": "mobile",
        "roots": ["delegated_manager_application/lib"],
        "exts": {".dart"},
        "sheet": "04_Delegated_Manager",
    },
    "sales_employee_application": {
        "kind": "mobile",
        "roots": ["sales_employee_application/lib"],
        "exts": {".dart"},
        "sheet": "05_Sales_Employee",
    },
    "sales_filter_application": {
        "kind": "mobile",
        "roots": ["sales_filter_application/lib"],
        "exts": {".dart"},
        "sheet": "06_Sales_Filter",
    },
    "follower_application": {
        "kind": "mobile",
        "roots": ["follower_application/lib"],
        "exts": {".dart"},
        "sheet": "07_Follower_App",
    },
    "mobile_app": {
        "kind": "mobile",
        "roots": ["mobile_app/lib"],
        "exts": {".dart"},
        "sheet": "10_mobile_app",
    },
    "MA_ShortCompany": {
        "kind": "mobile",
        "roots": ["MA_ShortCompany/lib"],
        "exts": {".dart"},
        "sheet": "11_MA_ShortCompany",
    },
    "BE_Company": {
        "kind": "backend",
        "roots": ["BE_Company"],
        "exts": {".cs"},
        "sheet": None,
    },
    "BE_SalesEmployee": {
        "kind": "backend",
        "roots": ["BE_SalesEmployee"],
        "exts": {".cs"},
        "sheet": None,
    },
    "BE_DelegateWebApplication": {
        "kind": "backend",
        "roots": ["BE_DelegateWebApplication/BE_DelegateWebApplication"],
        "exts": {".cs"},
        "sheet": None,
    },
}

AXIOS_RE = re.compile(
    r"""(?:axios|http)\.(get|post|put|patch|delete)\(\s*(`[^`]+`|'[^']+'|"[^"]+")""",
    re.IGNORECASE,
)
FETCH_RE = re.compile(r"""fetch\(\s*(`[^`]+`|'[^']+'|"[^"]+")""", re.IGNORECASE)
APICLIENT_RE = re.compile(
    r"""(?:ApiClient|apiClient|client)\.(get|post|put|patch|delete|getBytes)\(\s*(`[^`]+`|'[^']+'|"[^"]+")""",
    re.IGNORECASE,
)
SM_RE = re.compile(r"""\b(?:smGet|smPost|smPut|smDelete)\([^)]*?(`[^`]+`|'[^']+'|"[^"]+")""")
URI_PARSE_RE = re.compile(r"""Uri\.parse\(\s*(`[^`]+`|'[^']+'|"[^"]+"|\$\{[^}]+\}[^)]+)""")
DART_HTTP_RE = re.compile(
    r"""\b(?:http|dio)\.(get|post|put|patch|delete)\(\s*(?:Uri\.parse\()?(`[^`]+`|'[^']+'|"[^"]+")""",
    re.IGNORECASE,
)
CS_HTTP_RE = re.compile(
    r"""(?:HttpMethod\.(Get|Post|Put|Patch|Delete)|HttpMethod\("?(GET|POST|PUT|PATCH|DELETE)"?\))""",
    re.IGNORECASE,
)
PATH_LITERAL_RE = re.compile(
    r"""['"`]((?:api/|/api/|sales/|sales-manager/|sales-filter/|sales-gw/|Auth/|Users/|Customers/|Delegates/|Followers/|follower-|Portal/|mobile-updates/|delegate/|delegated-manager/|complaints|internal/|SalesEmployee/|CustomersSales/|CustomersPayments|Shift/|Track/|Ratings|Stores/|TrustReceipts/)[^'"`]*)"""
)
CS_OUTBOUND = re.compile(
    r"Send(Authorized|Manager|Filter|GatewayKey)Async|ProxyAssigned|ProxyByCity|HttpRequestMessage|"
    r"RelativePath|ForwardAsync|NormalizeLink|NormalizeBaseUrl",
    re.IGNORECASE,
)
FT_RE = re.compile(r"""\b(?:ftGet|ftPost)\(\s*(`[^`]+`|'[^']+'|"[^"]+")""")
DART_CONST_RE = re.compile(r"""(?:static\s+)?const\s+String\s+(\w+)\s*=\s*'([^']+)'""")
ATTR_LINE_RE = re.compile(r"^\s*\[(HttpGet|HttpPost|HttpPut|HttpPatch|HttpDelete|Route)\b")
INTERP_RE = re.compile(r"\$\{[^}]+\}|\$\w+")
NUMERIC_SEG_RE = re.compile(r"/(?:\d+)(?=/|$)")
KNOWN_PREFIXES = (
    "sales-manager/",
    "sales-filter/",
    "sales/",
    "auth/",
    "users/",
    "customers/",
    "delegates/",
    "followers/",
    "follower-lists/",
    "follower-tracking/",
    "portal/",
    "mobile-updates/",
    "delegate/",
    "delegated-manager/",
    "complaints",
    "internal/",
    "salesemployee/",
    "customerssales/",
    "customerspayments/",
    "customerspaymentsrequest/",
    "customerdecisions/",
    "ratings/",
    "stores/",
    "items/",
    "employees/",
    "suppliers/",
    "accounts/",
    "buys/",
    "backup/",
    "statistics/",
    "shift/",
    "track/",
    "trustreceipts/",
    "hubs/",
)


@dataclass
class RawConsumer:
    app: str
    kind: str
    method: str
    raw_path: str
    normalized_route: str
    source_file: str
    source_line: int
    function_name: str
    evidence: str


def _unquote(token: str) -> str:
    token = token.strip()
    if len(token) >= 2 and token[0] in "`'\"" and token[-1] == token[0]:
        return token[1:-1]
    return token


def canonicalize_consumer_path(raw: str, app: str, nearby: str = "") -> str | None:
    if not raw:
        return None
    value = raw.strip()
    value = value.replace("${apiUrl}", "/api/")
    value = value.replace("${salesGatewayBase()}", "/api/")
    value = value.replace("${DEMO_API}", "/api/")
    value = value.replace("${base}", "/api/")
    value = value.replace("${link}", "/api/")
    value = value.replace("${linkDelegate}", "/api/")
    value = value.replace("${selectedApi}", "/api/")
    value = re.sub(r"^\$\{[^}]+\}", "/api/", value)
    value = INTERP_RE.sub("{id}", value)
    value = value.replace("/{id}delegate/", "/delegate/")
    value = value.replace("/{id}delegates/", "/delegates/")
    value = value.replace("/{id}customers/", "/customers/")
    value = value.replace("/{id}followers/", "/followers/")
    value = value.replace("/{id}mobile-updates/", "/mobile-updates/")
    value = value.replace("/{id}auth/", "/auth/")
    value = value.replace("/{id}users/", "/users/")
    value = value.replace("/{id}trustreceipts/", "/trustreceipts/")
    value = re.sub(r"https?://[^/]+", "", value, flags=re.IGNORECASE)
    value = value.split("?")[0]
    value = value.replace("/sales-gw", "")
    value = value.replace("/api/api", "/api")
    value = value.replace("//", "/")
    # FE sales-manager helper prefixes
    if "salesManagerBase()" in nearby or "sales-manager/" in nearby:
        if not value.lower().startswith("/api/") and not value.lower().startswith("sales-manager"):
            if not any(value.lower().startswith(p) for p in ("http", "users/", "auth/")):
                value = "sales-manager/" + value.lstrip("/")
    if "followerTrackingBase()" in nearby or "follower-tracking/" in nearby:
        if not value.lower().startswith("/api/") and not value.lower().startswith("follower-tracking"):
            value = "follower-tracking/" + value.lstrip("/")
    if not value.startswith("/"):
        # relative to /api/
        if value.lower().startswith("api/"):
            value = "/" + value
        else:
            value = "/api/" + value.lstrip("/")
    if not value.lower().startswith("/api/") and not value.lower().startswith("/hubs/"):
        if any(seg in value.lower() for seg in ("sales-manager", "sales-filter", "/sales/")):
            value = "/api" + value if value.startswith("/") else "/api/" + value
        else:
            return None
    value = NUMERIC_SEG_RE.sub("/{id}", value)
    value = value.replace("{cityValue}", "{cityValue}")
    value = re.sub(r"\{[^}]+\}", lambda m: m.group(0), value)
    value = normalize_route(value)
    # Drop obviously non-API
    if value in {"/api", "/api/"}:
        return None
    if "composition-api" in value.lower():
        return None
    return value


def _guess_function(lines: list[str], idx: int) -> str:
    for j in range(idx, max(-1, idx - 40), -1):
        line = lines[j]
        m = re.search(
            r"(?:export\s+)?(?:async\s+)?(?:function\s+|const\s+|let\s+|var\s+|Future<[^>]+>\s+|void\s+|static\s+)(\w+)\s*(?:[=(]|=>)",
            line,
        )
        if m:
            return m.group(1)
        m = re.search(r"^\s*(?:public|private|protected)?\s*(?:async\s+)?[\w<>]+\s+(\w+)\s*\(", line)
        if m:
            return m.group(1)
    return ""


def _iter_files(repo_root: Path, roots: list[str], exts: set[str]):
    for root in roots:
        base = repo_root / root
        if not base.exists():
            continue
        for path in base.rglob("*"):
            if not path.is_file() or path.suffix not in exts:
                continue
            if any(part in SKIP_DIR_NAMES for part in path.parts):
                continue
            yield path


def scan_app(repo_root: Path, app: str, cfg: dict) -> list[RawConsumer]:
    found: list[RawConsumer] = []
    for path in _iter_files(repo_root, cfg["roots"], cfg["exts"]):
        rel = path.relative_to(repo_root).as_posix()
        try:
            text = path.read_text(encoding="utf-8-sig", errors="replace")
        except OSError:
            continue
        consts = dict(DART_CONST_RE.findall(text))
        expanded = text
        for name, val in consts.items():
            expanded = expanded.replace(f"${name}", val)
            expanded = re.sub(rf"\b{name}\b", f"'{val}'", expanded)
        lines = expanded.splitlines()
        orig_lines = text.splitlines()
        for i, line in enumerate(lines):
            nearby = "\n".join(lines[max(0, i - 3) : min(len(lines), i + 2)])
            method = ""
            raws: list[str] = []
            if path.suffix == ".cs" and ATTR_LINE_RE.search(line):
                continue
            for rx in (AXIOS_RE, APICLIENT_RE, DART_HTTP_RE):
                for match in rx.finditer(line):
                    method = match.group(1).upper().replace("GETBYTES", "GET")
                    raws.append(_unquote(match.group(2)))
            for match in FETCH_RE.finditer(line):
                method = method or "GET"
                raws.append(_unquote(match.group(1)))
            for match in SM_RE.finditer(line):
                name = match.group(0)
                method = "GET"
                if "Post" in name:
                    method = "POST"
                elif "Put" in name:
                    method = "PUT"
                elif "Delete" in name:
                    method = "DELETE"
                raws.append("sales-manager/" + _unquote(match.group(1)).lstrip("/"))
            for match in FT_RE.finditer(line):
                method = "POST" if "Post" in match.group(0) else "GET"
                raws.append("follower-tracking/" + _unquote(match.group(1)).lstrip("/"))
            for match in URI_PARSE_RE.finditer(line):
                raws.append(_unquote(match.group(1)))
                if re.search(r"http\.post|\.post\(", nearby, re.I):
                    method = method or "POST"
                elif re.search(r"http\.put|\.put\(", nearby, re.I):
                    method = method or "PUT"
                elif re.search(r"http\.delete|\.delete\(", nearby, re.I):
                    method = method or "DELETE"
                elif re.search(r"http\.get|\.get\(", nearby, re.I):
                    method = method or "GET"
            for match in PATH_LITERAL_RE.finditer(line):
                raws.append(match.group(1))

            if path.suffix == ".cs":
                window = "\n".join(orig_lines[max(0, i - 8) : min(len(orig_lines), i + 3)])
                if not CS_OUTBOUND.search(window):
                    raws = []
                else:
                    for match in re.finditer(
                        r'"(?:api/)?((?:internal/|sales-manager/|sales-filter/|sales/|SalesEmployee/|CustomersSales/|Users/)[^"]+)"',
                        line,
                    ):
                        raws.append(match.group(1))
                    m2 = CS_HTTP_RE.search(nearby)
                    if m2:
                        method = (m2.group(1) or m2.group(2) or method).upper()

            seen_local = set()
            for raw in raws:
                if raw in seen_local:
                    continue
                seen_local.add(raw)
                canon = canonicalize_consumer_path(raw, app, nearby)
                if not canon:
                    continue
                # Require a known prefix to reduce noise
                tail = canon[len("/api/") :] if canon.lower().startswith("/api/") else canon.lstrip("/")
                if not any(tail.startswith(p) or ("/" + tail).startswith("/" + p) for p in KNOWN_PREFIXES):
                    if not any(p.rstrip("/") in tail for p in KNOWN_PREFIXES):
                        continue
                found.append(
                    RawConsumer(
                        app=app,
                        kind=cfg["kind"],
                        method=method,
                        raw_path=raw,
                        normalized_route=canon,
                        source_file=rel,
                        source_line=i + 1,
                        function_name=_guess_function(lines, i),
                        evidence=line.strip()[:240],
                    )
                )
    return found


def scan_consumers(repo_root: Path) -> list[RawConsumer]:
    all_consumers: list[RawConsumer] = []
    for app, cfg in APPS.items():
        all_consumers.extend(scan_app(repo_root, app, cfg))
    return all_consumers


def route_template_regex(normalized_route: str) -> re.Pattern:
    parts = []
    for seg in normalized_route.strip("/").split("/"):
        if seg.startswith("{") and seg.endswith("}"):
            parts.append(r"[^/]+")
        else:
            parts.append(re.escape(seg))
    return re.compile("^/" + "/".join(parts) + "$", re.IGNORECASE)


def match_consumer_to_endpoints(consumer: RawConsumer, endpoints) -> list:
    matches = []
    cpath = consumer.normalized_route
    # Also try replacing {id} greedily
    for ep in endpoints:
        if ep.kind != "http":
            if cpath.lower().startswith("/hubs/"):
                matches.append(ep)
            continue
        if consumer.method and consumer.method not in {"", "GETBYTES"} and consumer.method != ep.http_method:
            # still allow unknown method
            if consumer.method not in {ep.http_method}:
                continue
        rx = route_template_regex(ep.normalized_route)
        # consumer may have concrete leftover names
        if rx.match(cpath):
            matches.append(ep)
            continue
        # Compare ignoring {param} vs {id}
        ep_gen = re.sub(r"\{[^}]+\}", "{id}", ep.normalized_route)
        c_gen = re.sub(r"\{[^}]+\}", "{id}", cpath)
        if ep_gen.lower() == c_gen.lower():
            matches.append(ep)
            continue
        # Consumer path is a prefix of the endpoint (list vs item) only when equal resource.
        if not consumer.method or consumer.method == ep.http_method:
            ep_l = ep.normalized_route.lower().rstrip("/")
            c_l = cpath.lower().rstrip("/")
            if ep_l == c_l:
                matches.append(ep)
    return matches
