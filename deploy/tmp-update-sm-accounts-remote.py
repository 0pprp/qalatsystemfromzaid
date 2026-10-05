#!/usr/bin/env python3
"""Apply on server: set SalesManagerAccounts drop-in for qalaa-sales-gateway."""
from __future__ import annotations

import json
import os
import subprocess
import urllib.error
import urllib.request

ACCOUNTS = [
    ("karrar", "karrar123", "مدير المبيعات كرار"),
    ("saad", "saad123", "مدير المبيعات سعد"),
    ("zai", "zai123", "مدير المبيعات زينب"),
    ("jin", "jin123", "مدير المبيعات جنان"),
    ("ayman", "ayman123", "مدير المبيعات ايمن"),
]

DROP_DIR = "/etc/systemd/system/qalaa-sales-gateway.service.d"
DROP_FILE = f"{DROP_DIR}/sales-manager-accounts.conf"


def main() -> int:
    os.makedirs(DROP_DIR, exist_ok=True)
    lines = ["[Service]"]
    # Clear old slots 0-9 first by overwriting with the five accounts only;
    # leftover fat/ban in main unit Environment= still apply UNLESS we override same keys.
    # Main unit currently uses indexes 0-3 for fat/zai/ban/jin — we overwrite 0-4 entirely.
    for i in range(0, 10):
        for key in ("UserName", "Password", "DisplayName"):
            full = f"SalesManagerAccounts__{i}__{key}"
            if i < len(ACCOUNTS):
                u, p, d = ACCOUNTS[i]
                val = {"UserName": u, "Password": p, "DisplayName": d}[key]
                # Quote values so Arabic display names with spaces survive systemd parsing.
                lines.append(f'Environment={full}="{val}"')
            else:
                # Neutralize leftover higher indexes from older configs
                lines.append(f'Environment={full}=""')

    open(DROP_FILE, "w", encoding="utf-8").write("\n".join(lines) + "\n")
    print("WROTE", DROP_FILE)
    print(open(DROP_FILE, encoding="utf-8").read())

    subprocess.check_call(["systemctl", "daemon-reload"])
    subprocess.check_call(["systemctl", "restart", "qalaa-sales-gateway"])
    print("ACTIVE", subprocess.check_output(["systemctl", "is-active", "qalaa-sales-gateway"], text=True).strip())

    import time

    for i in range(24):
        try:
            urllib.request.urlopen("http://127.0.0.1:5280/api/Auth/LoginSalesManager", timeout=3)
        except urllib.error.HTTPError:
            print("UP after", i)
            break
        except Exception as e:
            print("wait", i, e)
            time.sleep(5)
    else:
        print("STILL_DOWN")
        return 1

    env = subprocess.check_output(
        ["systemctl", "show", "qalaa-sales-gateway", "-p", "Environment", "--value"],
        text=True,
    )
    for name in ["karrar", "saad", "zai", "jin", "ayman", "fat", "ban"]:
        print("HAS", name, name in env)

    for u, p, d in ACCOUNTS:
        req = urllib.request.Request(
            "http://127.0.0.1:5280/api/Auth/LoginSalesManager",
            data=json.dumps({"userName": u, "password": p}).encode(),
            headers={"Content-Type": "application/json"},
        )
        try:
            with urllib.request.urlopen(req, timeout=20) as resp:
                body = json.loads(resp.read().decode())
                print("LOGIN_OK", u, "display=", body.get("userName"), "city=", body.get("cityName"))
        except urllib.error.HTTPError as e:
            print("LOGIN_FAIL", u, e.code, e.read()[:200])
        except Exception as e:
            print("LOGIN_FAIL", u, e)
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
