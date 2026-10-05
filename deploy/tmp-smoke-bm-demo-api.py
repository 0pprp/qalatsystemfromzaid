"""Smoke-test Najaf DEMO: login Users_LoginAdmin as مدير فرع if credentials available."""
from __future__ import annotations

import json
import os
import urllib.error
import urllib.request

API = "http://169.58.236.52:8080/api/"


def post(path: str, body: dict):
    req = urllib.request.Request(
        API + path,
        data=json.dumps(body).encode("utf-8"),
        headers={"Content-Type": "application/json", "Accept": "application/json"},
        method="POST",
    )
    with urllib.request.urlopen(req, timeout=30) as resp:
        return resp.status, json.loads(resp.read().decode("utf-8"))


def get(path: str, token: str):
    req = urllib.request.Request(
        API + path,
        headers={"Authorization": f"Bearer {token}", "Accept": "application/json"},
        method="GET",
    )
    with urllib.request.urlopen(req, timeout=45) as resp:
        return resp.status, json.loads(resp.read().decode("utf-8"))


def main() -> int:
    # Health / reachability
    try:
        req = urllib.request.Request(API.replace("/api/", "/") + "", method="GET")
        urllib.request.urlopen(req, timeout=10)
    except Exception:
        pass

    user = os.environ.get("BM_DEMO_USER", "").strip()
    password = os.environ.get("BM_DEMO_PASS", "").strip()
    if not user or not password:
        # Probe WeakWeekPayers without auth → expect 401
        try:
            get("CustomerDecisions/WeakWeekPayers", "invalid")
            print("UNEXPECTED 200 without auth")
            return 1
        except urllib.error.HTTPError as e:
            print("API_REACHABLE", API, "auth_gate", e.code)
            print("NO_CREDS set BM_DEMO_USER/BM_DEMO_PASS to full login smoke")
            return 0

    status, data = post("Users/Users_LoginAdmin", {"userName": user, "password": password})
    print("LOGIN", status, list(data.keys()))
    token = data.get("token")
    if not token:
        print("FAIL no token", data)
        return 1
    # decode user type roughly
    import base64

    payload = token.split(".")[1] + "=="
    claims = json.loads(base64.urlsafe_b64decode(payload.encode()))
    print("USER_TYPE", claims.get("UserType"))
    if claims.get("UserType") != "مدير فرع":
        print("FAIL not branch manager")
        return 1
    st, rows = get("CustomerDecisions/WeakWeekPayers", token)
    print("WEAK_PAYERS", st, "count", len(rows) if isinstance(rows, list) else type(rows))
    st2, hist = get("CustomerDecisions/Decisions", token)
    print("DECISIONS", st2, "count", len(hist) if isinstance(hist, list) else type(hist))
    print("OK")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
