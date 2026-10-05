#!/usr/bin/env python3
"""Ensure central SM account zai / zai123 / زينب on production + demo gateways (all provinces)."""
from __future__ import annotations

import json
import os
import textwrap

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

TARGET = ("zai", "zai123", "زينب")

UNITS = [
    ("qalaa-sales-gateway", "http://127.0.0.1:5280/api"),
    ("qalaa-demo-sales-gateway", "http://127.0.0.1:5403/api"),
]


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    remote = textwrap.dedent(
        f"""
        import json, os, re, subprocess, time, urllib.error, urllib.request

        TARGET = {TARGET!r}
        UNITS = {UNITS!r}
        MAX_SLOTS = 16

        def parse_accounts(env: str):
            # SalesManagerAccounts__N__UserName=... or Environment=KEY="val"
            by_i = {{}}
            # systemctl show Environment is space-separated KEY=VAL (quoted vals possible)
            # Also read drop-in file if present for accuracy.
            return by_i

        def read_dropin(unit: str):
            path = f'/etc/systemd/system/{{unit}}.service.d/sales-manager-accounts.conf'
            accounts = {{}}
            if not os.path.isfile(path):
                return accounts, path
            for line in open(path, encoding='utf-8'):
                line = line.strip()
                if not line.startswith('Environment=SalesManagerAccounts__'):
                    continue
                # Environment=SalesManagerAccounts__0__UserName="karrar"
                rest = line[len('Environment='):]
                if '=' not in rest:
                    continue
                key, val = rest.split('=', 1)
                val = val.strip()
                if len(val) >= 2 and val[0] == '"' and val[-1] == '"':
                    val = val[1:-1]
                m = re.match(r'SalesManagerAccounts__(\\d+)__(UserName|Password|DisplayName)$', key)
                if not m:
                    continue
                i = int(m.group(1))
                field = m.group(2)
                accounts.setdefault(i, {{}})[field] = val
            return accounts, path

        def accounts_to_list(accounts: dict):
            rows = []
            for i in sorted(accounts):
                a = accounts[i]
                u = (a.get('UserName') or '').strip()
                p = (a.get('Password') or '').strip()
                d = (a.get('DisplayName') or '').strip()
                if not u and not p and not d:
                    continue
                if not u:
                    continue
                rows.append((u, p, d))
            return rows

        def write_dropin(unit: str, rows):
            drop_dir = f'/etc/systemd/system/{{unit}}.service.d'
            os.makedirs(drop_dir, exist_ok=True)
            path = f'{{drop_dir}}/sales-manager-accounts.conf'
            lines = ['[Service]']
            for i in range(0, MAX_SLOTS):
                for key in ('UserName', 'Password', 'DisplayName'):
                    full = f'SalesManagerAccounts__{{i}}__{{key}}'
                    if i < len(rows):
                        u, p, d = rows[i]
                        val = {{'UserName': u, 'Password': p, 'DisplayName': d}}[key]
                        lines.append(f'Environment={{full}}="{{val}}"')
                    else:
                        lines.append(f'Environment={{full}}=""')
            open(path, 'w', encoding='utf-8').write('\\n'.join(lines) + '\\n')
            print('WROTE', path)
            for i, (u, p, d) in enumerate(rows):
                print(f'  [{{i}}] {{u}} / *** / {{d}}')
            return path

        def ensure_target(rows):
            tu, tp, td = TARGET
            out = []
            found = False
            for u, p, d in rows:
                if u.lower() == tu.lower():
                    out.append((tu, tp, td))
                    found = True
                else:
                    out.append((u, p, d))
            if not found:
                out.append((tu, tp, td))
            return out

        def login(base, user, password):
            req = urllib.request.Request(
                base + '/Auth/LoginSalesManager',
                data=json.dumps({{'userName': user, 'password': password}}).encode('utf-8'),
                headers={{'Content-Type': 'application/json; charset=utf-8'}},
            )
            with urllib.request.urlopen(req, timeout=30) as resp:
                return json.loads(resp.read().decode())

        touched = []
        for unit, base in UNITS:
            if subprocess.call(['systemctl', 'cat', unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
                print('SKIP_UNIT', unit)
                continue
            accounts, path = read_dropin(unit)
            rows = accounts_to_list(accounts)
            print('BEFORE', unit, rows)
            # If drop-in empty, try to salvage known managers from live Environment
            if not rows:
                env = subprocess.check_output(
                    ['systemctl', 'show', unit, '-p', 'Environment', '--value'],
                    text=True,
                )
                # crude fallback defaults used historically
                defaults = [
                    ('karrar', 'karrar123', 'مدير المبيعات كرار'),
                    ('saad', 'saad123', 'مدير المبيعات سعد'),
                    ('jin', 'jin123', 'مدير المبيعات جنان'),
                    ('ayman', 'ayman123', 'مدير المبيعات ايمن'),
                ]
                # keep defaults that still appear / always keep karrar+saad
                rows = list(defaults)
                if 'صاحب' in env or 'الكعبي' in env:
                    rows.append(('صاحب الكعبي', 'صاحب95', 'مدير المبيعات صاحب الكعبي'))
                print('FALLBACK_ROWS', unit, [r[0] for r in rows])

            rows = ensure_target(rows)
            write_dropin(unit, rows)
            touched.append((unit, base))

        subprocess.check_call(['systemctl', 'daemon-reload'])
        for unit, base in touched:
            subprocess.check_call(['systemctl', 'restart', unit])
            print('RESTARTED', unit, subprocess.check_output(['systemctl', 'is-active', unit], text=True).strip())

        time.sleep(5)

        tu, tp, td = TARGET
        for unit, base in touched:
            for i in range(24):
                try:
                    urllib.request.urlopen(base + '/Auth/LoginSalesManager', timeout=3)
                except urllib.error.HTTPError:
                    break
                except Exception:
                    time.sleep(2)
            try:
                body = login(base, tu, tp)
                print('LOGIN_OK', unit, 'display=', body.get('userName'), 'city=', body.get('cityName'), 'central=', body.get('central'), 'type=', body.get('userType'))
                if (body.get('userName') or '').strip() != td:
                    print('WARN_DISPLAY_MISMATCH', body.get('userName'), 'expected', td)
            except urllib.error.HTTPError as e:
                print('LOGIN_FAIL', unit, e.code, e.read()[:400].decode('utf-8', 'replace'))
            except Exception as e:
                print('LOGIN_FAIL', unit, e)
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/ensure_sm_zai.py", "w") as f:
                f.write(remote)
        _, o, e = c.exec_command("python3 /tmp/ensure_sm_zai.py", timeout=240)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-2000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
