#!/usr/bin/env python3
import os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
# recent image status codes
echo "=== nginx image statuses last 500 matches ==="
grep -E 'customer-documents|.*/shop-image' /var/log/nginx/access.log | tail -200 | awk '{print $9}' | sort | uniq -c | sort -rn

echo "=== sample 200 image lines ==="
grep -E 'customer-documents|.*/shop-image' /var/log/nginx/access.log | grep ' 200 ' | tail -20

echo "=== sample 404 image lines ==="
grep -E 'customer-documents|.*/shop-image' /var/log/nginx/access.log | grep ' 404 ' | tail -20

# Use docker for SQL if available
echo "=== docker ==="
docker ps --format '{{.Names}} {{.Image}}' 2>/dev/null | head -20

# Create tiny C# query using existing runtime
cat > /tmp/qdocs.csx <<'CS'
#r "nuget: Microsoft.Data.SqlClient, 5.2.2"
using Microsoft.Data.SqlClient;
var cs = "Data Source=127.0.0.1;Initial Catalog=DatabaseCompanyNajaf;User ID=haider;Password=haider12345@@@@@;TrustServerCertificate=True";
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
await using var cmd = conn.CreateCommand();
cmd.CommandText = @"SELECT Id, FileKey, CustomerId, DocumentType FROM dbo.SalesCustomerDocuments WHERE Id IN (2,3,4,5) OR CustomerId=53327;
SELECT SaleId, ShopImageKey FROM dbo.SalesShopProfiles WHERE SaleId=2 OR SaleId IN (SELECT SaleId FROM dbo.SalesDrafts WHERE CustomerId=53327);";
await using var r = await cmd.ExecuteReaderAsync();
do {
  Console.WriteLine("--- result set ---");
  while (await r.ReadAsync()) {
    for (int i=0;i<r.FieldCount;i++) Console.Write(r.GetName(i)+"="+r.GetValue(i)+" | ");
    Console.WriteLine();
  }
} while (await r.NextResultAsync());
CS

# Prefer dotnet-script if present; else use mssql in docker
if command -v dotnet-script >/dev/null; then
  dotnet-script /tmp/qdocs.csx
else
  echo "no dotnet-script"
fi

# check file existence for ids
python3 - <<'PY'
import os
base='/opt/qalaa-runtime/AdminNajaf/App_Data/sales/customer-docs'
print('doc count', len(os.listdir(base)) if os.path.isdir(base) else 0)
for i in [2,3,4,5,19,100,123,220]:
  for ext in ('.jpg','.jpeg','.png','.webp'):
    p=f'{base}/{i}{ext}'
    if os.path.isfile(p):
      print('FOUND', p, os.path.getsize(p))
print('sale dirs sample', sorted(os.listdir('/opt/qalaa-runtime/AdminNajaf/App_Data/sales'))[:20])
print('has sale 2', os.path.isdir('/opt/qalaa-runtime/AdminNajaf/App_Data/sales/2'))
PY
'''
_,o,e=c.exec_command(remote, timeout=120)
print(o.read().decode('utf-8', errors='replace')[-8000:])
err=e.read().decode('utf-8', errors='replace')
if err.strip():
    print('ERR', err[-1500:])
c.close()
