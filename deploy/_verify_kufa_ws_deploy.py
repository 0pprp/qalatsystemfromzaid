#!/usr/bin/env python3
"""Verify najaf delegate env + ResolveCityValue after WrongSale deploy."""
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    remote = r'''
import os, json, re, subprocess
from pathlib import Path

def mask(line):
    if '=' not in line:
        return line
    k, v = line.split('=', 1)
    if any(x in k.lower() for x in ('key', 'pass', 'secret', 'token')):
        return f'{k}=***'
    return line

print('active', subprocess.getoutput('systemctl is-active qalaa-delegatehaidernajaflast'))
pid = subprocess.getoutput('systemctl show -p MainPID --value qalaa-delegatehaidernajaflast').strip()
print('PID', pid)
if pid and pid != '0':
    for b in Path(f'/proc/{pid}/environ').read_bytes().split(b'\0'):
        s = b.decode('utf-8', 'replace')
        if any(k in s for k in ('SalesGateway', 'Branch__', 'Branch:')):
            print('PROC', mask(s))

dll = Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/BE_DelegateWebApplication.dll')
data = dll.read_bytes()
print('ResolveCityValue', b'ResolveCityValue' in data)
print('IsNajafFamilyLabel', b'IsNajafFamilyLabel' in data)
print('الكوفة bytes', 'الكوفة'.encode('utf-8') in data)

cfg = json.loads(Path('/opt/qalaa-runtime/DelegateHaiderNajafLast/appsettings.json').read_text(encoding='utf-8'))
cs = cfg['ConnectionStrings']['DataBaseConnection']

def grab(key):
    m = re.search(rf'(?:^|;){re.escape(key)}=([^;]+)', cs, re.I)
    return m.group(1) if m else ''

server = grab('Data Source') or grab('Server')
db = grab('Initial Catalog') or grab('Database')
user = grab('User ID') or grab('User Id')
pwd = grab('Password')
sqlcmd = '/opt/mssql-tools18/bin/sqlcmd' if os.path.isfile('/opt/mssql-tools18/bin/sqlcmd') else 'sqlcmd'

def run(sql):
    p = subprocess.run(
        [sqlcmd, '-C', '-f', '65001', '-S', server, '-d', db, '-U', user, '-P', pwd, '-W', '-s', '|', '-Q', sql],
        capture_output=True, text=True, encoding='utf-8', errors='replace',
    )
    return (p.stdout or '') + (p.stderr or '')

print('=== FirstCollection table ===')
print(run("SET NOCOUNT ON; SELECT CASE WHEN OBJECT_ID(N'dbo.FirstCollectionEvaluations',N'U') IS NULL THEN N'NO' ELSE N'YES' END AS HasFCE; SELECT CASE WHEN OBJECT_ID(N'dbo.DelegateFirstCollectionEvaluations',N'U') IS NULL THEN N'NO' ELSE N'YES' END AS HasDFCE;"))

print('=== Delegate 7 recent payments ===')
print(run("""
SET NOCOUNT ON;
SELECT TOP 10 PaymentID, CustomerID, DelegateID,
  CONVERT(varchar(19), PaymentDate, 120) AS PaymentDate
FROM dbo.CustomersPayments
WHERE DelegateID=7
ORDER BY PaymentDate DESC;
"""))

print('=== journal WrongSale since restart ===')
print(subprocess.getoutput(
    "journalctl -u qalaa-delegatehaidernajaflast --since '10 minutes ago' --no-pager | "
    "grep -iE 'WrongSale|SalesGateway|ResolveCity|مبيعات خاطئة|FirstCollection' | tail -30 || true"
))
print('OK')
'''

    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(
        HOST,
        username="root",
        password=SSH_PASS,
        timeout=60,
        allow_agent=False,
        look_for_keys=False,
    )
    try:
        sftp = c.open_sftp()
        path = "/tmp/verify_kufa_ws_deploy.py"
        with sftp.file(path, "w") as f:
            f.write(remote)
        sftp.close()
        _, stdout, stderr = c.exec_command(f"python3 {path}", timeout=120)
        out = stdout.read().decode("utf-8", "replace")
        err = stderr.read().decode("utf-8", "replace")
        print(out)
        if err.strip():
            print("STDERR", err[:1500], file=sys.stderr)
        Path(r"C:\GitHub\SalesHaider-main\SalesHaider-main\_ops_tmp_kufa_ws_verify.txt").write_text(
            out, encoding="utf-8"
        )
        return stdout.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
