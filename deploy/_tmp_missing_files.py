#!/usr/bin/env python3
import os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
cd /tmp/sqlq
cat > Program.cs <<'CS'
using Microsoft.Data.SqlClient;
var cs = "Data Source=127.0.0.1;Initial Catalog=DatabaseCompanyNajaf;User ID=haider;Password=${SQL_PASSWORD};TrustServerCertificate=True";
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
await using var cmd = new SqlCommand("SELECT Id, FileKey, ShopImageKey=NULL FROM dbo.SalesCustomerDocuments", conn);
// just docs
cmd.CommandText = "SELECT Id, FileKey FROM dbo.SalesCustomerDocuments";
var docs = new List<(int id, string key)>();
await using (var r = await cmd.ExecuteReaderAsync()) {
  while (await r.ReadAsync()) docs.Add((r.GetInt32(0), r.IsDBNull(1)?"":r.GetString(1)));
}
var root = "/opt/qalaa-runtime/AdminNajaf/App_Data";
int ok=0, miss=0;
var missing = new List<int>();
foreach (var (id, key) in docs) {
  var path = Path.IsPathRooted(key) ? key : Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar));
  if (File.Exists(path)) ok++; else { miss++; if (missing.Count<30) missing.Add(id); }
}
Console.WriteLine($"docs total={docs.Count} ok={ok} missing={miss}");
Console.WriteLine("missing sample: " + string.Join(",", missing));

cmd.CommandText = "SELECT SaleId, ShopImageKey FROM dbo.SalesShopProfiles";
var shops = new List<(int id, string key)>();
await using (var r = await cmd.ExecuteReaderAsync()) {
  while (await r.ReadAsync()) shops.Add((r.GetInt32(0), r.IsDBNull(1)?"":r.GetString(1)));
}
int sok=0,smiss=0; var sm=[];
foreach (var (id, key) in shops) {
  var path = string.IsNullOrWhiteSpace(key)?"": (Path.IsPathRooted(key)?key:Path.Combine(root, key.Replace('/', Path.DirectorySeparatorChar)));
  var folder = Path.Combine(root, "sales", id.ToString());
  var found = (!string.IsNullOrEmpty(path) && File.Exists(path)) || (Directory.Exists(folder) && Directory.GetFiles(folder,"shop.*").Length>0);
  if (found) sok++; else { smiss++; if (sm.Count<20) sm.Add(id); }
}
Console.WriteLine($"shops total={shops.Count} ok={sok} missing={smiss}");
Console.WriteLine("missing shops: " + string.Join(",", sm));

// check backups for missing images
Console.WriteLine("backup dirs:");
foreach (var d in Directory.GetDirectories("/opt/qalaa-backups").Take(20)) Console.WriteLine(d);
CS
dotnet run -c Release --nologo

echo "=== search backups for customer-docs ==="
find /opt/qalaa-backups -type d -name 'customer-docs' 2>/dev/null | head -20
find /opt -path '*customer-docs/2.jpg' 2>/dev/null | head -10
find /opt -path '*/sales/2/shop.jpg' 2>/dev/null | head -10
ls /opt/qalaa-backups 2>/dev/null | head -30
'''
_,o,e=c.exec_command(remote, timeout=300)
print(o.read().decode('utf-8', errors='replace')[-10000:])
err=e.read().decode('utf-8', errors='replace')
if err.strip():
    print('ERR', err[-1500:])
c.close()
