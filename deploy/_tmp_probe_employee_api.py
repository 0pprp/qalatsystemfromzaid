#!/usr/bin/env python3
import os
import sys
import urllib.request

import paramiko

sys.stdout.reconfigure(encoding="utf-8", errors="replace")
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(
    "169.58.236.52",
    username="root",
    password=os.environ["SSH_PASS"],
    timeout=40,
    allow_agent=False,
    look_for_keys=False,
)
cmd = r"""
systemctl is-active qalaa-company-sharenewnajaf
# hit local trend employee vs list
curl -sS -o /tmp/t_emp.json -w "emp_http=%{http_code} time=%{time_total}\n" \
  "http://127.0.0.1:$(systemctl show qalaa-company-sharenewnajaf -p Environment --value 2>/dev/null | tr ' ' '\n' | grep -i ASPNETCORE_URLS | head -1 | sed 's/.*://;s|/.*||')" \
  2>/dev/null || true
# discover listen port
ss -ltnp | grep -i BE_Company | head -5 || true
ss -ltnp | grep 105 | head -10 || true
# from unit file
systemctl cat qalaa-company-sharenewnajaf | head -40
# try common najaf ports / host header
for url in \
  'http://127.0.0.1:5000/api/sales-manager/stats/trend?scope=employee&id=7&fromDate=2026-09-17&toDate=2026-09-23' \
  'http://127.0.0.1:5280/api/sales-manager/stats/trend?scope=employee&id=7&fromDate=2026-09-17&toDate=2026-09-23' \
  'http://sharenewnajaf.alsaaeidy.com/api/sales-manager/stats/trend?scope=employee&id=7&fromDate=2026-09-17&toDate=2026-09-23'
do
  echo "TRY $url"
  curl -sS -m 15 -o /tmp/out.json -w "code=%{http_code} bytes=%{size_download}\n" "$url" || echo curl_fail
  head -c 400 /tmp/out.json; echo
done
# compare list scope
curl -sS -m 15 -o /tmp/out_list.json -w "list_code=%{http_code}\n" \
  'http://sharenewnajaf.alsaaeidy.com/api/sales-manager/stats/trend?scope=list&id=1&fromDate=2026-09-17&toDate=2026-09-23'
head -c 300 /tmp/out_list.json; echo
# dll integrity
python3 -c "from pathlib import Path; p=Path('/opt/qalaa-runtime/AdminNajaf/BE_Company.dll'); d=p.read_bytes(); print('size',len(d),'MZ',d[:2], 'PeriodAggregate', b'PeriodAggregate' in d)"
# compare dependency count vs publish
ls /opt/qalaa-runtime/AdminNajaf/*.dll | wc -l
"""
_, o, e = c.exec_command(cmd, timeout=90)
print(o.read().decode("utf-8", "replace")[-10000:])
print(e.read().decode("utf-8", "replace")[:1500])
c.close()
