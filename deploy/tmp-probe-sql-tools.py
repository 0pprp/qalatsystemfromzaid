#!/usr/bin/env python3
import os, paramiko
HOST="169.58.236.52"
SSH_PASS=os.environ["SSH_PASS"]
cmd = r"""
set -e
echo WHICH_PY=$(which python3)
python3 -c 'import pymssql; print("pymssql_ok")' 2>&1 || true
python3 -c 'import pyodbc; print("pyodbc_ok")' 2>&1 || true
ls /opt/mssql-tools*/bin/sqlcmd 2>/dev/null || true
ls /opt/mssql-tools18/bin/sqlcmd 2>/dev/null || true
which sqlcmd 2>/dev/null || true
find /usr/local /opt -name 'pymssql*' 2>/dev/null | head
pip3 show pymssql 2>&1 | head -3 || true
# how prior scripts ran
ls /root/*pymssql* 2>/dev/null || true
docker ps --format '{{.Names}}' 2>/dev/null | head
"""
c=paramiko.SSHClient(); c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
_,o,e=c.exec_command(cmd, timeout=60)
print(o.read().decode("utf-8","replace"))
print(e.read().decode("utf-8","replace")[:800])
c.close()
