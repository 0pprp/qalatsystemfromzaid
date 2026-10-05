"""
Generate docs/Qalaa_API_Endpoint_Registry.xlsx and .json from a static repository scan.

Usage (from repo root):
    py -3 tools/api_registry/generate_api_registry.py

This script does not start servers, deploy, or modify application runtime code.
"""

from __future__ import annotations

import json
import subprocess
import sys
from collections import defaultdict
from datetime import datetime, timedelta, timezone
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
sys.path.insert(0, str(Path(__file__).resolve().parent))

from enrich import enrich  # noqa: E402
from excel_export import build_workbook  # noqa: E402
from scan_consumers import APPS, match_consumer_to_endpoints, scan_consumers  # noqa: E402
from scan_csharp import RawEndpoint, scan_backends  # noqa: E402


def git_commit(repo: Path) -> str:
    try:
        return (
            subprocess.check_output(["git", "rev-parse", "HEAD"], cwd=repo, stderr=subprocess.DEVNULL)
            .decode()
            .strip()
        )
    except (subprocess.CalledProcessError, FileNotFoundError, OSError):
        return "UNKNOWN"


def json_ready(obj):
    if isinstance(obj, Path):
        return str(obj)
    if isinstance(obj, dict):
        return {k: json_ready(v) for k, v in obj.items()}
    if isinstance(obj, list):
        return [json_ready(v) for v in obj]
    return obj


def consumer_is_client(app: str) -> bool:
    return APPS.get(app, {}).get("kind") in {"web", "mobile"}


def attach_matches(endpoints: list[RawEndpoint], consumers) -> dict:
    """Map id(endpoint) -> matching consumer records (unique by app+file+line)."""
    matches = defaultdict(list)
    # Index endpoints by method for speed
    by_method = defaultdict(list)
    for ep in endpoints:
        by_method[ep.http_method].append(ep)
        by_method[""].append(ep)

    for c in consumers:
        pool = by_method.get(c.method, []) if c.method else endpoints
        if not c.method:
            pool = endpoints
        else:
            pool = by_method.get(c.method, []) + [e for e in endpoints if e.kind == "signalr"]
        hits = match_consumer_to_endpoints(c, pool)
        # Prefer exact service if consumer is a backend talking to a known peer
        for ep in hits:
            matches[id(ep)].append(c)
    return matches


def build_canonical_map(rows: list[dict], groups: list[dict]) -> list[dict]:
    out = []
    by_fn = defaultdict(list)
    for r in rows:
        by_fn[r["businessFunction"]].append(r)
    for g in groups:
        fn = g["businessFunction"]
        members = by_fn[fn]
        publics = [
            m
            for m in members
            if "/internal/" not in m["normalizedRoute"].lower() and m["kind"] == "http"
        ]
        internals = [m for m in members if "/internal/" in m["normalizedRoute"].lower()]
        legacy = [
            m
            for m in members
            if m["status"] == "LEGACY"
            or any(tok in m["normalizedRoute"].lower() for tok in ("/api/shift", "/api/track", "/salesemployee/", "/create"))
        ]
        owner = "BE_SalesEmployee" if internals else (publics[0]["service"] if publics else members[0]["service"])
        public_c = g["recommendedCanonicalEndpoint"]
        if publics:
            # Prefer non-legacy public
            preferred = [m for m in publics if m not in legacy] or publics
            public_c = f"{preferred[0]['method']} {preferred[0]['normalizedRoute']}"
        internal_c = ""
        if internals:
            internal_c = f"{internals[0]['method']} {internals[0]['normalizedRoute']}"
        clients = sorted({a for m in members for a in m.get("calledByApps", []) if consumer_is_client(a)})
        rule = (
            "Do not create another endpoint for this function on the same trust boundary. "
            + g["recommendedAction"]
        )
        out.append(
            {
                "businessFunction": fn,
                "current": " | ".join(f"{m['method']} {m['normalizedRoute']} [{m['service']}]" for m in members),
                "publicCanonical": public_c,
                "internalCanonical": internal_c,
                "owner": owner,
                "clients": "; ".join(clients),
                "legacy": " | ".join(f"{m['method']} {m['normalizedRoute']}" for m in legacy if m not in internals),
                "futureRule": rule,
            }
        )
    out.sort(key=lambda r: r["businessFunction"])
    return out


def main() -> int:
    iraq = timezone(timedelta(hours=3))
    generated = datetime.now(iraq).strftime("%Y-%m-%d %H:%M:%S %z")
    commit = git_commit(ROOT)

    endpoints, scan_stats = scan_backends(ROOT)
    consumers = scan_consumers(ROOT)
    matches = attach_matches(endpoints, consumers)
    rows, groups, chains = enrich(endpoints, consumers, matches)

    web_apps = {k for k, v in APPS.items() if v["kind"] == "web"}
    mobile_apps = {k for k, v in APPS.items() if v["kind"] == "mobile"}

    by_service = defaultdict(int)
    by_method = defaultdict(int)
    for r in rows:
        by_service[r["service"]] += 1
        by_method[r["method"]] += 1
    for method in ("GET", "POST", "PUT", "PATCH", "DELETE", "SIGNALR"):
        by_method.setdefault(method, 0)

    by_client = defaultdict(set)
    mobile_only = web_only = shared = 0
    no_client = 0
    unauth = 0
    internal = 0
    for r in rows:
        clients = [a for a in r.get("calledByApps", []) if consumer_is_client(a)]
        for a in clients:
            by_client[a].add(r["endpointId"])
        has_web = any(a in web_apps for a in clients)
        has_mobile = any(a in mobile_apps for a in clients)
        if not clients:
            no_client += 1
        elif has_web and has_mobile:
            shared += 1
        elif has_mobile:
            mobile_only += 1
        elif has_web:
            web_only += 1
        if not r.get("authRequired"):
            unauth += 1
        if r.get("status") == "ACTIVE-INTERNAL" or "/internal/" in r["normalizedRoute"].lower():
            internal += 1

    class_counts = defaultdict(int)
    for g in groups:
        class_counts[g["classification"]] += 1
    true_dup_groups = [g for g in groups if g["classification"].startswith("E.")]
    high_risk = [g for g in groups if g["riskScore"] >= 4]

    orphans = []
    for r in rows:
        clients = [a for a in r.get("calledByApps", []) if consumer_is_client(a)]
        if clients:
            continue
        rec = "Do not delete. Marked NO STATIC CONSUMER FOUND — may be used by production apps, other provinces, or operators."
        if r["duplicateClassification"].startswith("D."):
            rec = "Likely legacy alias. Keep until mobile/web callers are confirmed gone, then retire behind a flag."
        if r["duplicateClassification"].startswith("E."):
            rec = "Review whether this extra route on the same trust boundary can stop being advertised. Do not delete yet."
        if "/internal/" in r["normalizedRoute"].lower():
            rec = "Internal S2S. Absence of app callers is expected."
        orphans.append({**r, "recommendation": rec})

    app_sheets = {}
    for app, cfg in APPS.items():
        if cfg.get("sheet") and any(app in r.get("calledByApps", []) for r in rows):
            app_sheets[app] = cfg["sheet"]

    service_sheets = {}
    order = ["BE_Company", "BE_SalesEmployee", "BE_DelegateWebApplication"]
    codes = ["20_BE_Company", "21_BE_SalesEmployee", "22_BE_Delegate"]
    for svc, code in zip(order, codes):
        if any(r["service"] == svc for r in rows):
            service_sheets[svc] = code

    canonical = build_canonical_map(rows, groups)

    payload = {
        "meta": {
            "generatedAt": generated,
            "commit": commit,
            "repository": str(ROOT),
            "scanStats": {
                "controllerFiles": scan_stats["controller_files"],
                "controllerFileCount": len(scan_stats["controller_files"]),
                "httpAttributeCount": scan_stats["http_attribute_count"],
                "extractedEndpointCount": len(endpoints),
                "hubFiles": scan_stats["hub_files"],
                "consumerCount": len(consumers),
            },
            "appSheets": app_sheets,
            "serviceSheets": service_sheets,
            "governance": {
                "rule": "One business capability should have one canonical public entry point per trust boundary.",
                "beforeAddingEndpoint": [
                    "Search Qalaa_API_Endpoint_Registry.xlsx",
                    "Search code by Business Function",
                    "Search DuplicateGroupId",
                    "Check if an existing endpoint can be extended/reused",
                    "Check service ownership",
                    "Check whether a bridge endpoint already exists",
                    "Only add a new endpoint if existing routes cannot support the requirement cleanly",
                    "Document justification",
                    "Update the registry",
                ],
            },
        },
        "summary": {
            "totalEndpoints": len(rows),
            "byMethod": dict(by_method),
            "byService": dict(by_service),
            "byClientApp": {k: len(v) for k, v in sorted(by_client.items())},
            "businessFunctionCount": len({r["businessFunction"] for r in rows}),
            "duplicateGroupCount": len(groups),
            "classificationCounts": dict(class_counts),
            "trueDuplicateGroupCount": len(true_dup_groups),
            "highRiskGroupCount": len(high_risk),
            "noClientConsumerCount": no_client,
            "unauthenticatedCount": unauth,
            "internalCount": internal,
            "mobileOnlyCount": mobile_only,
            "webOnlyCount": web_only,
            "sharedCount": shared,
        },
        "endpoints": rows,
        "duplicateGroups": groups,
        "callChains": chains,
        "orphans": orphans,
        "canonicalMap": canonical,
        "consumers": [
            {
                "app": c.app,
                "kind": c.kind,
                "method": c.method,
                "rawPath": c.raw_path,
                "normalizedRoute": c.normalized_route,
                "sourceFile": c.source_file,
                "sourceLine": c.source_line,
                "functionName": c.function_name,
            }
            for c in consumers
        ],
    }

    docs = ROOT / "docs"
    docs.mkdir(parents=True, exist_ok=True)
    json_path = docs / "Qalaa_API_Endpoint_Registry.json"
    xlsx_path = docs / "Qalaa_API_Endpoint_Registry.xlsx"
    json_path.write_text(json.dumps(json_ready(payload), ensure_ascii=False, indent=2), encoding="utf-8")
    build_workbook(payload, xlsx_path)

    print(f"Endpoints extracted: {len(rows)}")
    print(f"HTTP attributes scanned: {scan_stats['http_attribute_count']}")
    print(f"Controller files: {len(scan_stats['controller_files'])}")
    print(f"JSON: {json_path}")
    print(f"Excel: {xlsx_path}")
    print(f"Commit: {commit}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
