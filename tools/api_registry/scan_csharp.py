"""Static extraction of ASP.NET Core controller and hub endpoints."""

from __future__ import annotations

import re
from dataclasses import dataclass, field
from pathlib import Path

HTTP_METHODS = ("Get", "Post", "Put", "Patch", "Delete")
HTTP_ATTR_RE = re.compile(
    r"\[Http(Get|Post|Put|Patch|Delete)(?:\s*\((.*?)\))?\s*\]",
    re.IGNORECASE | re.DOTALL,
)
ROUTE_ATTR_RE = re.compile(r"\[Route\s*\((.*?)\)\s*\]", re.IGNORECASE | re.DOTALL)
AUTHORIZE_RE = re.compile(r"\[Authorize(?:\s*\((.*?)\))?\s*\]", re.IGNORECASE | re.DOTALL)
ALLOW_ANON_RE = re.compile(r"\[AllowAnonymous\]", re.IGNORECASE)
CLASS_RE = re.compile(
    r"(?:public|internal|protected|private)?\s*(?:sealed\s+)?(?:partial\s+)?"
    r"class\s+(\w+)\s*(?::\s*([^{\n]+))?",
)
METHOD_RE = re.compile(
    r"public\s+(?:static\s+)?(?:async\s+)?(?:[\w.<>,\[\]?]+\s+)+\s*(\w+)\s*\("
)
STRING_LIT_RE = re.compile(r'"((?:\\.|[^"\\])*)"')
POLICY_RE = re.compile(r"Policy\s*=\s*(?:SalesPolicies\.)?(\w+)")
ROLES_RE = re.compile(r"Roles\s*=\s*\"([^\"]+)\"")
XML_SUMMARY_RE = re.compile(r"<summary>(.*?)</summary>", re.DOTALL)
PARAM_CONSTRAINT_RE = re.compile(r"\{([^}:]+):[^}]+\}")
DOWNSTREAM_PATH_RE = re.compile(
    r'"(?:api/)?((?:internal/|sales-manager/|sales-filter/|sales/|SalesEmployee/|'
    r'CustomersSales/|Users/|Auth/)[^"?\s]*)"'
)

BACKEND_ROOTS = (
    "BE_Company",
    "BE_SalesEmployee",
    "BE_DelegateWebApplication",
)

SKIP_DIR_NAMES = {
    "bin",
    "obj",
    ".git",
    "node_modules",
    "BE_Company.Tests",
    "BE_Company.Sales.Tests",
    "BE_SalesEmployee.Tests",
    "BE_DelegateWebApplication.Tests",
}

POLICY_ROLES = {
    "AnySales": "موظف مبيعات, مدير مبيعات",
    "SalesEmployee": "موظف مبيعات",
    "SalesManager": "مدير مبيعات",
    "SalesFilterEmployee": "موظف فلترة المبيعات",
    "DelegatedManager": "مدير مفوض",
    "SalesManagerOrDelegatedManager": "مدير مبيعات, مدير مفوض",
    "ReadGps": "مدير مبيعات",
    "ReadOtherSalesEmployees": "مدير مبيعات",
    "ReadAllBranchSales": "مدير مبيعات",
    "ReadFollowerGps": "محاسب / قراءة GPS للمتابعين",
    "MainAccountant": "محاسب رئيسي",
    "SearchAllBranches": "موظف أو مدير مبيعات",
    "WriteOwnBranch": "موظف أو مدير مبيعات",
}


@dataclass
class RawEndpoint:
    service: str
    project: str
    controller: str
    action: str
    http_method: str
    controller_route: str
    action_template: str
    full_route: str
    normalized_route: str
    auth_required: bool
    auth_type: str
    allowed_roles: str
    internal_key: str
    branch_central_scope: str
    xml_summary: str
    injected_types: list[str]
    downstream_hints: list[str]
    source_file: str
    source_line: int
    kind: str = "http"  # http | signalr
    notes: str = ""


def _strip_line_comment(line: str) -> str:
    in_str = False
    out = []
    i = 0
    while i < len(line):
        ch = line[i]
        if ch == '"' and (i == 0 or line[i - 1] != "\\"):
            in_str = not in_str
            out.append(ch)
        elif not in_str and ch == "/" and i + 1 < len(line) and line[i + 1] == "/":
            if not line[i : i + 3] == "///":
                break
            out.append(ch)
        else:
            out.append(ch)
        i += 1
    return "".join(out)


def _first_string(blob: str | None) -> str:
    if not blob:
        return ""
    match = STRING_LIT_RE.search(blob)
    return match.group(1) if match else ""


def _join_route(controller_route: str, action_template: str, controller_token: str) -> str:
    cr = (controller_route or "").replace("[controller]", controller_token).strip("/")
    at = (action_template or "").strip("/")
    if at.startswith("~/"):
        path = at[2:]
    elif not cr:
        path = at
    elif not at:
        path = cr
    else:
        path = f"{cr}/{at}"
    path = "/" + path.lstrip("/")
    path = re.sub(r"/{2,}", "/", path)
    return path


def normalize_route(route: str) -> str:
    if not route:
        return ""
    path = route.split("?")[0]
    path = PARAM_CONSTRAINT_RE.sub(r"{\1}", path)
    path = re.sub(r"/{2,}", "/", path)
    if path != "/" and path.endswith("/"):
        path = path[:-1]
    # ASP.NET routing is case-insensitive; keep a stable identity.
    parts = []
    for seg in path.split("/"):
        if seg.startswith("{") and seg.endswith("}"):
            name = seg[1:-1].split(":")[0]
            parts.append("{" + name + "}")
        else:
            parts.append(seg.lower())
    return "/".join(parts)


def controller_token(class_name: str) -> str:
    if class_name.endswith("Controller"):
        return class_name[: -len("Controller")]
    return class_name


def service_for_path(rel: str) -> str:
    if rel.startswith("BE_Company"):
        return "BE_Company"
    if rel.startswith("BE_SalesEmployee"):
        return "BE_SalesEmployee"
    if rel.startswith("BE_DelegateWebApplication"):
        return "BE_DelegateWebApplication"
    return Path(rel).parts[0]


def _auth_from_attrs(attr_blob: str, class_auth: dict, body_blob: str) -> tuple[bool, str, str, str]:
    allow_anon = bool(ALLOW_ANON_RE.search(attr_blob)) or class_auth.get("anonymous")
    method_auth = AUTHORIZE_RE.findall(attr_blob)
    has_method_auth = bool(method_auth) or "[Authorize]" in attr_blob.replace(" ", "")
    class_required = class_auth.get("required", False)
    class_type = class_auth.get("type", "")
    class_roles = class_auth.get("roles", "")

    roles = ""
    auth_type = ""
    required = False

    if allow_anon:
        required = False
        auth_type = "Anonymous"
    elif has_method_auth:
        required = True
        auth_type = "JWT Bearer"
        for raw in method_auth:
            policy = POLICY_RE.search(raw or "")
            roles_m = ROLES_RE.search(raw or "")
            if policy:
                auth_type = f"JWT Bearer + Policy {policy.group(1)}"
                roles = POLICY_ROLES.get(policy.group(1), policy.group(1))
            if roles_m:
                roles = roles_m.group(1)
        if not roles and class_roles:
            roles = class_roles
    elif class_required:
        required = True
        auth_type = class_type or "JWT Bearer"
        roles = class_roles
    else:
        required = False
        auth_type = "None declared"

    internal_key = ""
    blob = attr_blob + "\n" + body_blob
    if "X-Sales-Gateway-Key" in blob or "GatewayKeyHeader" in blob or "HasValidGatewayKey" in blob or "IsGatewayRequest" in blob:
        internal_key = "X-Sales-Gateway-Key"
        if not required:
            auth_type = "Internal gateway key (optional JWT fallback possible)"
            required = True
    if "X-Async-Id" in blob or "AsyncIdHeader" in blob or "DelegateComplaintAuth" in blob:
        internal_key = (internal_key + "; " if internal_key else "") + "X-Async-Id / X-Delegate-Id"
        auth_type = "Delegate header credentials (X-Async-Id)"
        required = True
    if "ValidateToken" in blob or "PortalSettings" in blob or "encrypted token" in blob.lower():
        auth_type = "Encrypted portal query token"
        required = True
        roles = "Customer (token)"

    return required, auth_type, roles, internal_key


def _scope_for(service: str, route: str, auth_type: str) -> str:
    route_l = route.lower()
    if "/internal/" in route_l:
        return "Central internal (service-to-service)"
    if service == "BE_SalesEmployee":
        if "cityvalue" in route_l or "{cityvalue}" in route_l:
            return "Central gateway, branch-addressed"
        return "Central gateway"
    if service == "BE_DelegateWebApplication":
        return "Branch (delegate/follower)"
    if service == "BE_Company":
        if "sales-manager" in route_l or "sales-filter" in route_l or "/api/sales" in route_l:
            return "Branch (sales module)"
        return "Branch (company ERP)"
    return ""


def _collect_attr_blob(lines: list[str], start_idx: int) -> tuple[str, int, str]:
    """Walk backwards from a class/method line collecting attributes and xml summary."""
    attrs = []
    summaries = []
    i = start_idx - 1
    while i >= 0:
        raw = lines[i].strip()
        if not raw:
            i -= 1
            continue
        if raw.startswith("///"):
            summaries.append(raw[3:].strip())
            i -= 1
            continue
        if raw.startswith("["):
            chunk = raw
            j = i
            while chunk.count("[") > chunk.count("]") and j + 1 < start_idx:
                j += 1
                chunk += " " + lines[j].strip()
            attrs.append(chunk)
            i -= 1
            continue
        break
    attrs.reverse()
    summaries.reverse()
    summary_xml = " ".join(summaries)
    match = XML_SUMMARY_RE.search(" ".join(summaries))
    summary = re.sub(r"\s+", " ", match.group(1)).strip() if match else re.sub(r"\s+", " ", summary_xml).strip()
    return " ".join(attrs), i, summary


def _injected_types(class_body: str) -> list[str]:
    types = []
    for match in re.finditer(r"(?:private|protected|internal|public)\s+readonly\s+([\w.<>]+)\s+_", class_body):
        types.append(match.group(1))
    ctor = re.search(r"public\s+\w+\s*\((.*?)\)\s*\{", class_body, re.DOTALL)
    if ctor:
        for part in ctor.group(1).split(","):
            part = part.strip()
            if not part:
                continue
            tokens = part.split()
            if tokens:
                types.append(tokens[0])
    seen = set()
    out = []
    for t in types:
        if t not in seen and t[0:1].isupper():
            seen.add(t)
            out.append(t)
    return out


def _method_body(lines: list[str], method_idx: int) -> str:
    text = "\n".join(lines[method_idx : min(len(lines), method_idx + 80)])
    return text


def parse_controller_file(path: Path, repo_root: Path) -> list[RawEndpoint]:
    rel = path.relative_to(repo_root).as_posix()
    service = service_for_path(rel)
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    lines = text.splitlines()
    endpoints: list[RawEndpoint] = []

    for idx, line in enumerate(lines):
        stripped = _strip_line_comment(line).strip()
        class_match = CLASS_RE.search(stripped)
        if not class_match:
            continue
        class_name = class_match.group(1)
        bases = class_match.group(2) or ""
        is_controller = (
            "ControllerBase" in bases
            or "Controller" in bases
            or class_name.endswith("Controller")
        )
        is_hub = "Hub" in bases and "IHubContext" not in bases
        if not is_controller and not is_hub:
            continue

        attr_blob, _, class_summary = _collect_attr_blob(lines, idx)
        controller_route = ""
        for route_raw in ROUTE_ATTR_RE.findall(attr_blob):
            controller_route = _first_string(route_raw) or controller_route
        class_auth_required = bool(AUTHORIZE_RE.search(attr_blob)) and not ALLOW_ANON_RE.search(attr_blob)
        class_roles = ""
        class_type = "JWT Bearer" if class_auth_required else ""
        for raw in AUTHORIZE_RE.findall(attr_blob):
            policy = POLICY_RE.search(raw or "")
            if policy:
                class_type = f"JWT Bearer + Policy {policy.group(1)}"
                class_roles = POLICY_ROLES.get(policy.group(1), policy.group(1))
        class_auth = {
            "required": class_auth_required,
            "type": class_type,
            "roles": class_roles,
            "anonymous": bool(ALLOW_ANON_RE.search(attr_blob)),
        }

        # class body until next top-level class of similar indent or EOF
        class_indent = len(line) - len(line.lstrip())
        end = len(lines)
        for j in range(idx + 1, len(lines)):
            other = lines[j]
            other_stripped = other.strip()
            if not other_stripped or other_stripped.startswith("//") or other_stripped.startswith("["):
                continue
            other_indent = len(other) - len(other.lstrip())
            if other_indent <= class_indent and CLASS_RE.search(_strip_line_comment(other).strip()):
                end = j
                break
        class_body = "\n".join(lines[idx:end])
        injected = _injected_types(class_body)
        token = controller_token(class_name)

        if is_hub:
            required, auth_type, roles, internal_key = _auth_from_attrs(attr_blob, class_auth, class_body)
            endpoints.append(
                RawEndpoint(
                    service=service,
                    project=service,
                    controller=class_name,
                    action="OnConnectedAsync",
                    http_method="SIGNALR",
                    controller_route=controller_route,
                    action_template="",
                    full_route="/hubs/sales-tracking",
                    normalized_route="/hubs/sales-tracking",
                    auth_required=required,
                    auth_type=auth_type or "JWT Bearer",
                    allowed_roles=roles,
                    internal_key=internal_key,
                    branch_central_scope=_scope_for(service, "/hubs/sales-tracking", auth_type),
                    xml_summary=class_summary or "SignalR hub for live sales-manager GPS updates",
                    injected_types=injected,
                    downstream_hints=[],
                    source_file=rel,
                    source_line=idx + 1,
                    kind="signalr",
                )
            )
            continue

        for midx in range(idx + 1, end):
            mline = _strip_line_comment(lines[midx]).strip()
            method_match = METHOD_RE.search(mline)
            if not method_match:
                continue
            action_name = method_match.group(1)
            if action_name in {class_name, "if", "return", "using"}:
                continue
            mattr, _, summary = _collect_attr_blob(lines, midx)
            http_hits = list(HTTP_ATTR_RE.finditer(mattr))
            if not http_hits:
                continue
            body = _method_body(lines, midx)
            required, auth_type, roles, internal_key = _auth_from_attrs(
                mattr, class_auth, body + "\n" + class_body
            )
            downstream = sorted({m.group(1) for m in DOWNSTREAM_PATH_RE.finditer(body)})
            for hit in http_hits:
                method = hit.group(1).upper()
                template = _first_string(hit.group(2))
                extra_routes = ROUTE_ATTR_RE.findall(mattr)
                if not template and extra_routes:
                    template = _first_string(extra_routes[0])
                if service == "BE_DelegateWebApplication":
                    blob = f"{template} {controller_route} {mline} {body} {class_body}"
                    if "asyncid" in blob.lower() or "DelegateComplaintAuth" in class_body:
                        if not internal_key:
                            internal_key = "X-Async-Id or path/query asyncId"
                        if auth_type in {"None declared", ""}:
                            auth_type = "Delegate asyncId credential"
                        required = True
                        if not roles:
                            roles = "مندوب / متابع"
                full = _join_route(controller_route or f"api/{token}", template, token)
                notes = []
                if class_summary:
                    notes.append(class_summary)
                endpoints.append(
                    RawEndpoint(
                        service=service,
                        project=service,
                        controller=class_name,
                        action=action_name,
                        http_method=method,
                        controller_route=controller_route,
                        action_template=template,
                        full_route=full,
                        normalized_route=normalize_route(full),
                        auth_required=required,
                        auth_type=auth_type,
                        allowed_roles=roles,
                        internal_key=internal_key,
                        branch_central_scope=_scope_for(service, full, auth_type),
                        xml_summary=summary,
                        injected_types=injected,
                        downstream_hints=downstream,
                        source_file=rel,
                        source_line=midx + 1,
                        notes=" | ".join(notes),
                    )
                )
    return endpoints


def count_http_attributes(path: Path) -> int:
    text = path.read_text(encoding="utf-8-sig", errors="replace")
    return len(re.findall(r"\[Http(Get|Post|Put|Patch|Delete)\b", text))


def scan_backends(repo_root: Path) -> tuple[list[RawEndpoint], dict]:
    endpoints: list[RawEndpoint] = []
    stats = {
        "controller_files": [],
        "http_attribute_count": 0,
        "hub_files": [],
        "skipped": [],
    }
    for root_name in BACKEND_ROOTS:
        root = repo_root / root_name
        if not root.exists():
            stats["skipped"].append(root_name)
            continue
        for path in root.rglob("*.cs"):
            if any(part in SKIP_DIR_NAMES for part in path.parts):
                continue
            rel = path.relative_to(repo_root).as_posix()
            is_controller_name = "Controller" in path.name
            text_head = path.read_text(encoding="utf-8-sig", errors="replace")[:4000]
            is_hub = "Hub" in path.name and ": Hub" in path.read_text(encoding="utf-8-sig", errors="replace")
            looks_like_controller = is_controller_name or "[ApiController]" in text_head
            if not looks_like_controller and not is_hub:
                if "[HttpGet" in text_head or "[HttpPost" in text_head:
                    stats["skipped"].append(rel)
                continue
            found = parse_controller_file(path, repo_root)
            if is_controller_name or found:
                stats["controller_files"].append(rel)
                stats["http_attribute_count"] += count_http_attributes(path)
            if is_hub:
                stats["hub_files"].append(rel)
            endpoints.extend(found)

    # MapHub paths from Program.cs
    for program in repo_root.glob("**/Program.cs"):
        if any(part in SKIP_DIR_NAMES for part in program.parts):
            continue
        text = program.read_text(encoding="utf-8-sig", errors="replace")
        for match in re.finditer(r'MapHub<([^>]+)>\("([^"]+)"\)', text):
            route = match.group(2)
            for ep in endpoints:
                if ep.kind == "signalr" and match.group(1).endswith(ep.controller):
                    ep.full_route = route
                    ep.normalized_route = normalize_route(route)
                    ep.source_file = program.relative_to(repo_root).as_posix()
                    # keep controller line as evidence too via notes
                    ep.notes = (ep.notes + " | " if ep.notes else "") + f"MapHub in {program.name}"

    endpoints.sort(key=lambda e: (e.service, e.normalized_route, e.http_method, e.controller, e.action))
    return endpoints, stats
