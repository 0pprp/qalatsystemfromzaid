#!/usr/bin/env python3
import os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
set -e
# find tools
which python3; ls /usr/bin/pip* 2>/dev/null; ls /opt/mssql-tools*/bin/sqlcmd 2>/dev/null
# use docker mssql if available
docker ps --format '{{.Names}}' 2>/dev/null | head
# try python -m pip
python3 -m pip install -q pymssql 2>/tmp/piperr.txt || true
python3 <<'PY'
import os,sys
try:
    import pymssql
except Exception as e:
    print('NO_PYMSSQL', e)
    # fallback: use powershell? or node?
    sys.exit(0)

conn = pymssql.connect(server='127.0.0.1', user='haider', password=os.environ['SQL_PASSWORD'], database='DatabaseCompanyNajaf')
cur = conn.cursor(as_dict=True)
print('--- docs 2-5 ---')
cur.execute('SELECT Id, FileKey, ContentType, CustomerId, FileName FROM dbo.SalesCustomerDocuments WHERE Id IN (2,3,4,5)')
for r in cur.fetchall(): print(r)
print('--- docs customer 53327 ---')
cur.execute('SELECT Id, FileKey, DocumentType, CustomerId FROM dbo.SalesCustomerDocuments WHERE CustomerId=53327')
for r in cur.fetchall(): print(r)
print('--- any docs matching name phone from drafts ---')
cur.execute("""
SELECT d.Id, d.FileKey, d.CustomerId, d.CustomerName, d.CustomerPhone, d.DocumentType
FROM dbo.SalesCustomerDocuments d
WHERE d.CustomerId=53327 OR d.CustomerName LIKE N'%53327%'
""")
for r in cur.fetchall(): print(r)
# Also list docs that match via sale
cur.execute('SELECT SaleId, FullName, Phone FROM dbo.SalesDrafts WHERE CustomerId=53327')
sales=cur.fetchall(); print('sales', sales)
if sales:
    names=list({s['FullName'] for s in sales if s.get('FullName')})
    phones=list({s['Phone'] for s in sales if s.get('Phone')})
    print('names', names, 'phones', phones)
    cur.execute('SELECT TOP 20 Id, FileKey, CustomerId, CustomerName, CustomerPhone, SaleId, DocumentType FROM dbo.SalesCustomerDocuments ORDER BY Id')
    all_docs=cur.fetchall()
    # print docs that might match this customer by naive filter
    for r in all_docs:
        if r.get('CustomerId')==53327 or (r.get('CustomerName') in names) or (r.get('CustomerPhone') in phones) or (r.get('SaleId') in {s['SaleId'] for s in sales}):
            print('MATCH', r)
print('--- shop ---')
cur.execute('SELECT SaleId, ShopImageKey FROM dbo.SalesShopProfiles WHERE SaleId IN (2) OR SaleId IN (SELECT SaleId FROM dbo.SalesDrafts WHERE CustomerId=53327)')
for r in cur.fetchall(): print(r)
conn.close()
for id_ in [2,3,4,5,100,123]:
    p=f'/opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs/{id_}.jpg'
    print('exists', id_, os.path.isfile(p), 'size', os.path.getsize(p) if os.path.isfile(p) else None)
print('sale2', os.path.isdir('/opt/qalaa-runtime/AdminNajaf/App_Data/sales/2'), os.listdir('/opt/qalaa-runtime/AdminNajaf/App_Data/sales/2') if os.path.isdir('/opt/qalaa-runtime/AdminNajaf/App_Data/sales/2') else None)
PY
cat /tmp/piperr.txt 2>/dev/null | tail -20
'''
_,o,e=c.exec_command(remote, timeout=180)
print(o.read().decode('utf-8', errors='replace'))
err=e.read().decode('utf-8', errors='replace')
if err.strip():
    print('ERR', err[-2000:])
c.close()
