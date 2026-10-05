#!/usr/bin/env python3
"""Update qalaa-sales-gateway systemd env with 5 named sales-manager accounts."""
from __future__ import annotations

import os
import re
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

# Login user / password / DisplayName shown in JWT + actions
ACCOUNTS = [
    ("karrar", "karrar123", "مدير المبيعات كرار"),
    ("saad", "saad123", "مدير المبيعات سعد"),
    ("zai", "zai123", "مدير المبيعات زينب"),  # keep existing login
    ("jin", "jin123", "مدير المبيعات جنان"),  # keep existing login
    ("ayman", "ayman123", "مدير المبيعات ايمن"),
]


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    # Build env fragment (indexes 0..)
    lines = []
    for i, (u, p, d) in enumerate(ACCOUNTS):
        lines.append(f"SalesManagerAccounts__{i}__UserName={u}")
        lines.append(f"SalesManagerAccounts__{i}__Password={p}")
        lines.append(f"SalesManagerAccounts__{i}__DisplayName={d}")
    fragment = "\n".join(lines)

    remote = f"""
import os, re, subprocess, shutil, tempfile
unit_paths = [
  '/etc/systemd/system/qalaa-sales-gateway.service',
  '/lib/systemd/system/qalaa-sales-gateway.service',
  '/usr/lib/systemd/system/qalaa-sales-gateway.service',
]
unit = next((p for p in unit_paths if os.path.isfile(p)), None)
print('UNIT', unit)
text = open(unit, encoding='utf-8', errors='replace').read()
# Drop any existing SalesManagerAccounts__N__* from Environment=
def scrub_env_line(line: str) -> str:
    if not line.startswith('Environment='):
        return line
    # Keep tokens that are NOT SalesManagerAccounts__
    body = line[len('Environment='):].strip()
    # Values may contain spaces inside DisplayName — systemd stores as Environment=KEY=VAL KEY2=VAL2
    # Safer: rewrite from systemctl show Environment then rebuild.
    return line

# Prefer rewriting via drop-in override (cleanest)
drop_dir = '/etc/systemd/system/qalaa-sales-gateway.service.d'
os.makedirs(drop_dir, exist_ok=True)
# Clear old indexes 0-20 then set new
clear_lines = []
for i in range(0, 21):
    for k in ('UserName','Password','DisplayName'):
        clear_lines.append(f'SalesManagerAccounts__{{i}}__{{k}}=')
override = '''[Service]
# Clear previous additional SM accounts then set the five named managers.
''' + '\\n'.join(f'Environment={{x}}' for x in clear_lines) + '\\n' + '''
''' + '\\n'.join(f'Environment={{l}}' for l in {lines!r}) + '\\n'

# Actually empty assignment may not unset in systemd. Better: replace full Environment= line.
# Read current Environment from systemctl and rebuild.
env = subprocess.check_output(['systemctl','show','qalaa-sales-gateway','-p','Environment','--value'], text=True).strip()
# Split env preserving DisplayName with spaces: systemd shows as KEY=VAL pairs separated by space,
# but DisplayName values have spaces. Use regex on known prefixes.

# Parse by known keys pattern
keys = re.findall(r'(?:^|\\s)([A-Za-z0-9_]+)=', env)
# Extract values with a sequential parser
parts = {{}}
pattern = re.compile(r'([A-Za-z0-9_]+)=')
matches = list(pattern.finditer(env))
for idx, m in enumerate(matches):
    key = m.group(1)
    start = m.end()
    end = matches[idx+1].start() if idx+1 < len(matches) else len(env)
    parts[key] = env[start:end].rstrip()

# Remove all SalesManagerAccounts__*
parts = {{k:v for k,v in parts.items() if not k.startswith('SalesManagerAccounts__')}}

# Keep primary salesmanager
# Add five accounts
new_accounts = {ACCOUNTS!r}
for i,(u,p,d) in enumerate(new_accounts):
    parts[f'SalesManagerAccounts__{{i}}__UserName'] = u
    parts[f'SalesManagerAccounts__{{i}}__Password'] = p
    parts[f'SalesManagerAccounts__{{i}}__DisplayName'] = d

# Write override that sets ONLY the SalesManagerAccounts keys (and clear old 0-9)
ov = ['[Service]']
for i in range(0, 10):
    for k in ('UserName','Password','DisplayName'):
        key = f'SalesManagerAccounts__{{i}}__{{k}}'
        if key in parts:
            ov.append(f'Environment={{key}}={{parts[key]}}')
        else:
            # unset leftover higher slots by empty — skip; old fat/ban will remain in main unit
            pass

# To fully replace, override must unset old ones. systemd Environment= in drop-in ADDS/overrides same key.
# So set fat/ban slots to the new five only at 0-4, and clear 5+ if any.
# Also override fat/ban usernames by overwriting indexes 0-3 currently used.

open(f'{{drop_dir}}/sales-manager-accounts.conf','w',encoding='utf-8').write('\\n'.join(ov)+'\\n')
print('WROTE', f'{{drop_dir}}/sales-manager-accounts.conf')
print(open(f'{{drop_dir}}/sales-manager-accounts.conf',encoding='utf-8').read())
subprocess.check_call(['systemctl','daemon-reload'])
subprocess.check_call(['systemctl','restart','qalaa-sales-gateway'])
print('ACTIVE', subprocess.check_output(['systemctl','is-active','qalaa-sales-gateway'], text=True).strip())
# Verify env
env2 = subprocess.check_output(['systemctl','show','qalaa-sales-gateway','-p','Environment','--value'], text=True)
for name in ['karrar','saad','zai','jin','ayman','fat','ban']:
    print('HAS', name, name in env2)
# Login smoke tests (no password print)
import json, urllib.request
for u,p,d in new_accounts:
    req=urllib.request.Request('http://127.0.0.1:5280/api/Auth/LoginSalesManager', data=json.dumps({{'userName':u,'password':p}}).encode(), headers={{'Content-Type':'application/json'}})
    try:
        with urllib.request.urlopen(req, timeout=20) as resp:
            body=json.loads(resp.read().decode())
            print('LOGIN_OK', u, 'userName=', body.get('userName'), 'city=', body.get('cityName'))
    except Exception as e:
        print('LOGIN_FAIL', u, e)
"""
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        with c.open_sftp() as sftp:
            with sftp.file("/tmp/upd_sm_accounts.py", "w") as f:
                f.write(remote)
        stdin, stdout, stderr = c.exec_command("python3 /tmp/upd_sm_accounts.py", timeout=120)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        try:
            sys.stdout.reconfigure(encoding="utf-8", errors="replace")
        except Exception:
            pass
        sys.stdout.buffer.write((out + ("\nSTDERR " + err[:2000] if err.strip() else "") + "\n").encode("utf-8", "replace"))
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
