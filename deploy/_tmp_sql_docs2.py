#!/usr/bin/env python3
import os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
python3 <<'PY'
import subprocess, json
try:
    import pymssql
except ImportError:
    subprocess.check_call(['pip3', 'install', '-q', 'pymssql'])
    import pymssql

conn = pymssql.connect(server='127.0.0.1', user='haider', password=os.environ['SQL_PASSWORD'], database='DatabaseCompanyNajaf')
cur = conn.cursor(as_dict=True)
print('--- docs 2-5 ---')
cur.execute('SELECT Id, FileKey, ContentType, CustomerId, FileName FROM dbo.SalesCustomerDocuments WHERE Id IN (2,3,4,5)')
for r in cur.fetchall():
    print(r)
print('--- sample recent docs ---')
cur.execute('SELECT TOP 5 Id, FileKey, CustomerId FROM dbo.SalesCustomerDocuments ORDER BY Id DESC')
for r in cur.fetchall():
    print(r)
print('--- docs for customer 53327 ---')
cur.execute('SELECT Id, FileKey, DocumentType, CustomerId FROM dbo.SalesCustomerDocuments WHERE CustomerId=53327')
for r in cur.fetchall():
    print(r)
print('--- shop sale 2 ---')
cur.execute('SELECT SaleId, ShopImageKey FROM dbo.SalesShopProfiles WHERE SaleId=2')
print(cur.fetchall())
print('--- sales for customer 53327 ---')
cur.execute('SELECT TOP 10 SaleId, CustomerId, Status, FullName FROM dbo.SalesDrafts WHERE CustomerId=53327 ORDER BY SaleId DESC')
for r in cur.fetchall():
    print(r)
conn.close()

import os
for id_ in [2,3,4,5,100,123]:
    p=f'/opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs/{id_}.jpg'
    print('exists', p, os.path.isfile(p))
print('sale2 shop', os.path.isfile('/opt/qalaa-runtime/AdminNajaf/App_Data/sales/2/shop.jpg'))
print('sale64 shop', os.path.isfile('/opt/qalaa-runtime/AdminNajaf/App_Data/sales/64/shop.jpg'))
PY
'''
_,o,e=c.exec_command(remote, timeout=120)
print(o.read().decode('utf-8', errors='replace'))
err=e.read().decode('utf-8', errors='replace')
if err.strip():
    print('ERR', err[-1500:])
c.close()
