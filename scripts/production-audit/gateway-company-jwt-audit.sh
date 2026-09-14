#!/usr/bin/env bash
# READ-ONLY Production Gateway / Company JWT readiness audit.
# Prints YES/NO and fingerprints (first 12 hex of SHA-256) — never key values.
# Usage (on gateway + company host with config files readable):
#   GATEWAY_APPSETTINGS=/path/to/BE_SalesEmployee/appsettings.Production.json \
#   COMPANY_APPSETTINGS_GLOB='/path/to/branches/*/appsettings.Production.json' \
#   ./gateway-company-jwt-audit.sh

set -euo pipefail
umask 077

GATEWAY_APPSETTINGS="${GATEWAY_APPSETTINGS:?set GATEWAY_APPSETTINGS}"

python3 - <<'PY'
import hashlib, json, os, glob, sys

def load(path):
    with open(path, encoding="utf-8") as f:
        return json.load(f)

def keys_from(doc):
    try:
        arr = doc["Authentication"]["Schemes"]["Bearer"]["SigningKeys"]
    except Exception:
        return []
    out = []
    for item in arr or []:
        v = (item or {}).get("Value") or ""
        if v.strip():
            out.append(v.strip())
    return out

def fp12(b64: str) -> str:
    # Fingerprint the configured secret string bytes (not decoded) for equality checks only.
    return hashlib.sha256(b64.encode("utf-8")).hexdigest()[:12]

gw_path = os.environ["GATEWAY_APPSETTINGS"]
gw = load(gw_path)
gw_keys = keys_from(gw)
internal = (gw.get("InternalApiKey") or gw.get("SalesEmployee", {}).get("GatewayKey") or "").strip()
require_demo = gw.get("SalesManagement", {}).get("RequireDemoDatabase")

print(f"CompanyJwtSigningKeysConfigured={'YES' if gw_keys else 'NO'}")
print(f"KeyCount={len(gw_keys)}")
print(f"GatewayRequireDemoDatabase={require_demo}")
print(f"InternalApiKeyConfigured={'YES' if internal else 'NO'}")
if gw_keys:
    print("GatewayKeyFingerprints=" + ",".join(fp12(k) for k in gw_keys))

pattern = os.environ.get("COMPANY_APPSETTINGS_GLOB", "")
if not pattern:
    print("ALL_BRANCH_JWT_KEYS_MATCH=SKIPPED_NO_GLOB")
    sys.exit(0)

paths = sorted(glob.glob(pattern))
if not paths:
    print("ALL_BRANCH_JWT_KEYS_MATCH=NO_FILES")
    sys.exit(1)

fps = []
for p in paths:
    keys = keys_from(load(p))
    fps.append(tuple(fp12(k) for k in keys))
    print(f"BranchFile={os.path.basename(p)} KeyCount={len(keys)} Fingerprints={','.join(fp12(k) for k in keys) if keys else '-'}")

gw_fp = tuple(fp12(k) for k in gw_keys)
match = all(fp == gw_fp for fp in fps) and len(gw_fp) > 0
print(f"ALL_BRANCH_JWT_KEYS_MATCH={'YES' if match else 'NO'}")
sys.exit(0 if match else 2)
PY
