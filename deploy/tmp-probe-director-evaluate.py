#!/usr/bin/env python3
"""Probe director evaluate + evaluation-hits after deploy."""
import json
import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r'''
import json, urllib.request, urllib.error

port = 5280
body = json.dumps({"userName": "حيدر", "password": "حيدر"}).encode()
req = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/Auth/LoginDirector",
    data=body,
    headers={"Content-Type": "application/json"},
    method="POST",
)
with urllib.request.urlopen(req, timeout=20) as r:
    login = json.loads(r.read().decode())
token = login["token"]
print("login", login.get("userType"), bool(token))

# fetch a few requests
req2 = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/sales-manager/sales-requests?page=1&pageSize=5&tab=all",
    headers={"Authorization": "Bearer " + token},
)
with urllib.request.urlopen(req2, timeout=120) as r2:
    page = json.loads(r2.read().decode())
items = page.get("items") or []
print("list", page.get("total"), "sample", len(items))
if not items:
    raise SystemExit("no items")

payload_items = []
for row in items:
    rid = row.get("id") or row.get("Id")
    city = row.get("cityValue") or row.get("CityValue") or ""
    payload_items.append({
        "requestId": rid,
        "sourceCityValue": city,
        "customerName": row.get("customerName") or row.get("CustomerName") or "",
        "customerPhone": row.get("customerPhone") or row.get("CustomerPhone") or "",
        "key": f"{city}:{rid}",
    })

eval_body = json.dumps({"items": payload_items}).encode()
req3 = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/sales-manager/sales-requests/evaluate",
    data=eval_body,
    headers={"Authorization": "Bearer " + token, "Content-Type": "application/json"},
    method="POST",
)
try:
    with urllib.request.urlopen(req3, timeout=120) as r3:
        ev = json.loads(r3.read().decode())
    ev_items = ev.get("items") or ev.get("Items") or []
    print("EVAL_OK", len(ev_items))
    non_zero = 0
    for it in ev_items:
        # look for any category count
        cats = it.get("categories") or it.get("Categories") or []
        if isinstance(cats, dict):
            for v in cats.values():
                c = (v or {}).get("count") or (v or {}).get("Count") or 0
                if c:
                    non_zero += 1
        elif isinstance(cats, list):
            for v in cats:
                c = (v or {}).get("count") or (v or {}).get("Count") or 0
                if c:
                    non_zero += 1
        score = it.get("overallScore") if it.get("overallScore") is not None else it.get("OverallScore")
        label = it.get("overallRatingLabel") or it.get("OverallRatingLabel")
        print("  item", it.get("requestId") or it.get("RequestId"), "score", score, "label", label)
    print("NONZERO_CATS", non_zero)
except urllib.error.HTTPError as e:
    print("EVAL_FAIL", e.code, e.read()[:500].decode("utf-8", "replace"))
    raise SystemExit(1)

# try evaluation-hits on first item for FatherGrandfather / Phone-ish categories
row0 = items[0]
rid = row0.get("id") or row0.get("Id")
city = row0.get("cityValue") or row0.get("CityValue") or ""
for cat in ["FatherGrandfather", "Phone", "fatherGrandfather", "phone", "NameFatherGrandfather", "SimilarPhone"]:
    hit_body = json.dumps({
        "requestId": rid,
        "sourceCityValue": city,
        "customerName": row0.get("customerName") or row0.get("CustomerName") or "",
        "customerPhone": row0.get("customerPhone") or row0.get("CustomerPhone") or "",
        "category": cat,
        "page": 1,
        "pageSize": 10,
    }).encode()
    req4 = urllib.request.Request(
        f"http://127.0.0.1:{port}/api/sales-manager/sales-requests/evaluation-hits",
        data=hit_body,
        headers={"Authorization": "Bearer " + token, "Content-Type": "application/json"},
        method="POST",
    )
    try:
        with urllib.request.urlopen(req4, timeout=120) as r4:
            hits = json.loads(r4.read().decode())
        print("HITS", cat, "total", hits.get("total") or hits.get("Total"), "items", len(hits.get("items") or hits.get("Items") or []))
        break
    except urllib.error.HTTPError as e:
        body = e.read()[:300].decode("utf-8", "replace")
        print("HITS_TRY", cat, e.code, body[:120])

# mutating post must still 403
req5 = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/sales-manager/sales-requests/mark-all-read",
    data=b"{}",
    headers={"Authorization": "Bearer " + token, "Content-Type": "application/json"},
    method="POST",
)
try:
    urllib.request.urlopen(req5, timeout=30)
    print("MUTATE_UNEXPECTED_OK")
except urllib.error.HTTPError as e:
    print("MUTATE_BLOCKED", e.code)
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
try:
    sftp = c.open_sftp()
    with sftp.file("/tmp/probe_dir_eval.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/probe_dir_eval.py", timeout=300)
    print(out.read().decode("utf-8", "replace").encode("ascii", "backslashreplace").decode("ascii"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e.encode("ascii", "replace").decode("ascii")[:800])
    raise SystemExit(out.channel.recv_exit_status())
finally:
    c.close()
