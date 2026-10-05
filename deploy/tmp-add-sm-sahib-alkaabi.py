#!/usr/bin/env python3
"""Add sales-manager account صاحب الكعبي (all provinces) on production + demo gateways."""
from __future__ import annotations

import json
import os
import textwrap

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

# Keep existing managers + append صاحب
ACCOUNTS = [
    ("karrar", "karrar123", "مدير المبيعات كرار"),
    ("saad", "saad123", "مدير المبيعات سعد"),
    ("zai", "zai123", "مدير المبيعات زينب"),
    ("jin", "jin123", "مدير المبيعات جنان"),
    ("ayman", "ayman123", "مدير المبيعات ايمن"),
    ("صاحب الكعبي", "صاحب95", "مدير المبيعات صاحب الكعبي"),
]

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
        import json, os, subprocess, time, urllib.error, urllib.request

        ACCOUNTS = {ACCOUNTS!r}
        UNITS = {UNITS!r}

        def write_dropin(unit: str) -> str:
            drop_dir = f'/etc/systemd/system/{{unit}}.service.d'
            os.makedirs(drop_dir, exist_ok=True)
            path = f'{{drop_dir}}/sales-manager-accounts.conf'
            lines = ['[Service]']
            for i in range(0, 12):
                for key in ('UserName', 'Password', 'DisplayName'):
                    full = f'SalesManagerAccounts__{{i}}__{{key}}'
                    if i < len(ACCOUNTS):
                        u, p, d = ACCOUNTS[i]
                        val = {{'UserName': u, 'Password': p, 'DisplayName': d}}[key]
                        # Quote so Arabic + spaces survive systemd Environment parsing.
                        lines.append(f'Environment={{full}}="{{val}}"')
                    else:
                        lines.append(f'Environment={{full}}=""')
            open(path, 'w', encoding='utf-8').write('\\n'.join(lines) + '\\n')
            print('WROTE', path)
            return path

        for unit, _ in UNITS:
            if subprocess.call(['systemctl', 'cat', unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
                print('SKIP_UNIT', unit)
                continue
            write_dropin(unit)

        subprocess.check_call(['systemctl', 'daemon-reload'])
        for unit, _ in UNITS:
            if subprocess.call(['systemctl', 'cat', unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
                continue
            subprocess.check_call(['systemctl', 'restart', unit])
            print('RESTARTED', unit, subprocess.check_output(['systemctl', 'is-active', unit], text=True).strip())

        time.sleep(4)

        target_u, target_p, target_d = ACCOUNTS[-1]
        for unit, base in UNITS:
            if subprocess.call(['systemctl', 'cat', unit], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL) != 0:
                continue
            # wait up
            for i in range(20):
                try:
                    urllib.request.urlopen(base + '/Auth/LoginSalesManager', timeout=3)
                except urllib.error.HTTPError:
                    break
                except Exception as e:
                    time.sleep(2)
            req = urllib.request.Request(
                base + '/Auth/LoginSalesManager',
                data=json.dumps({{'userName': target_u, 'password': target_p}}).encode('utf-8'),
                headers={{'Content-Type': 'application/json; charset=utf-8'}},
            )
            try:
                with urllib.request.urlopen(req, timeout=30) as resp:
                    body = json.loads(resp.read().decode())
                print('LOGIN_OK', unit, 'display=', body.get('userName'), 'city=', body.get('cityName'), 'type=', body.get('userType'), 'central=', body.get('central'))
            except urllib.error.HTTPError as e:
                print('LOGIN_FAIL', unit, e.code, e.read()[:300].decode('utf-8', 'replace'))
            except Exception as e:
                print('LOGIN_FAIL', unit, e)

            # sanity: existing karrar still works
            req2 = urllib.request.Request(
                base + '/Auth/LoginSalesManager',
                data=json.dumps({{'userName': 'karrar', 'password': 'karrar123'}}).encode(),
                headers={{'Content-Type': 'application/json'}},
            )
            try:
                with urllib.request.urlopen(req2, timeout=20) as resp:
                    body = json.loads(resp.read().decode())
                print('KARRAR_OK', unit, body.get('userName'))
            except Exception as e:
                print('KARRAR_FAIL', unit, e)
        """
    )

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/add_sm_sahib.py", "w") as f:
                f.write(remote)
        i, o, e = c.exec_command("python3 /tmp/add_sm_sahib.py", timeout=180)
        print(o.read().decode("utf-8", "replace"))
        err = e.read().decode("utf-8", "replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
