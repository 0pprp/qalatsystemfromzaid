#!/usr/bin/env python3
"""DelegateNajaf API errors around payment sync window tonight."""
from __future__ import annotations

import os
import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ["SSH_PASS"]

REMOTE = r"""
echo '=== PostPayment / idempotent / 400 since Sep24 16:00 ==='
journalctl -u qalaa-delegatehaidernajaflast.service --no-pager --since '2026-09-24 16:00:00' 2>/dev/null | grep -iE 'PostPayment|Idempotent|CustomersPayments|fail|error|Exception|400|422' | tail -80
echo '=== Collection auto-posted Sep24 ==='
journalctl -u qalaa-company-sharenewnajaf.service --no-pager --since '2026-09-24 00:00:00' 2>/dev/null | grep -iE 'Collection|auto-posted|PostEligible' | tail -40
echo '=== nginx access PostPaymentIdempotent najaf today ==='
grep -h 'PostPaymentIdempotent' /var/log/nginx/access.log /var/log/nginx/*access*.log 2>/dev/null | grep -i najaf | tail -30
grep -h 'PostPaymentIdempotent' /var/log/nginx/access.log 2>/dev/null | tail -40
ls /var/log/nginx/ 2>/dev/null | head -20
"""

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect(HOST, username="root", password=SSH_PASS, timeout=40, allow_agent=False, look_for_keys=False)
_, out, err = c.exec_command(REMOTE, timeout=60)
print(out.read().decode("utf-8", "replace"))
e = err.read().decode("utf-8", "replace")
if e.strip():
    print("ERR", e[-1000:])
c.close()
