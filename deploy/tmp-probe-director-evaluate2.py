#!/usr/bin/env python3
"""Dump evaluate shape + find rows with matches for director."""
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
    data=body, headers={"Content-Type": "application/json"}, method="POST",
)
with urllib.request.urlopen(req, timeout=20) as r:
    token = json.loads(r.read().decode())["token"]

req2 = urllib.request.Request(
    f"http://127.0.0.1:{port}/api/sales-manager/sales-requests?page=1&pageSize=30&tab=all",
    headers={"Authorization": "Bearer " + token},
)
with urllib.request.urlopen(req2, timeout=120) as r2:
    page = json.loads(r2.read().decode())
items = page.get("items") or []
print("total", page.get("total"), "page_items", len(items))

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
with urllib.request.urlopen(req3, timeout=180) as r3:
    ev = json.loads(r3.read().decode())
ev_items = ev.get("items") or ev.get("Items") or []
print("eval_count", len(ev_items))
if ev_items:
    print("KEYS", sorted(ev_items[0].keys()))
    print("SAMPLE_JSON", json.dumps(ev_items[0], ensure_ascii=False)[:1200])

interesting = []
for it in ev_items:
    blob = json.dumps(it, ensure_ascii=False)
    # any numeric count > 0
    has = False
    for k, v in it.items():
        if isinstance(v, dict):
            c = v.get("count") or v.get("Count") or v.get("matchCount") or v.get("MatchCount")
            if isinstance(c, (int, float)) and c > 0:
                has = True
        if isinstance(v, (int, float)) and k.lower().endswith("count") and v > 0:
            has = True
    label = it.get("overallRatingLabel") or it.get("OverallRatingLabel") or ""
    if has or (label and label not in ("لا يوجد تطابق", "لا يوجد")):
        interesting.append(it)

print("interesting", len(interesting))
for it in interesting[:8]:
    print("---")
    print(json.dumps(it, ensure_ascii=False)[:900])

# hits with correct FE keys on first interesting or first item
target = interesting[0] if interesting else (ev_items[0] if ev_items else None)
if target:
    rid = target.get("requestId") or target.get("RequestId")
    city = target.get("sourceCityValue") or target.get("SourceCityValue") or ""
    # find matching list row for name/phone
    row = next((x for x in items if (x.get("id") or x.get("Id")) == rid), items[0])
    city = city or row.get("cityValue") or row.get("CityValue") or ""
    for cat in ["fatherGrandfather", "phone", "tripleName"]:
        hit_body = json.dumps({
            "requestId": rid,
            "sourceCityValue": city,
            "customerName": row.get("customerName") or row.get("CustomerName") or "",
            "customerPhone": row.get("customerPhone") or row.get("CustomerPhone") or "",
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
            print("HITS", cat, "total", hits.get("total") or hits.get("Total"),
                  "n", len(hits.get("items") or hits.get("Items") or []),
                  "keys", list((hits.get("items") or hits.get("Items") or [{}])[0].keys())[:8] if (hits.get("items") or hits.get("Items")) else [])
        except urllib.error.HTTPError as e:
            print("HITS_FAIL", cat, e.code, e.read()[:200].decode("utf-8", "replace"))
'''

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
try:
    sftp = c.open_sftp()
    with sftp.file("/tmp/probe_dir_eval2.py", "w") as f:
        f.write(REMOTE)
    sftp.close()
    _, out, err = c.exec_command("python3 /tmp/probe_dir_eval2.py", timeout=300)
    print(out.read().decode("utf-8", "replace").encode("ascii", "backslashreplace").decode("ascii"))
    e = err.read().decode("utf-8", "replace")
    if e.strip():
        print("STDERR", e.encode("ascii", "replace").decode("ascii")[:800])
    raise SystemExit(out.channel.recv_exit_status())
finally:
    c.close()
