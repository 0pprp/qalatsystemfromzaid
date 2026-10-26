#!/usr/bin/env python3
import os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko

c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

remote = r'''
set -e
IMG=$(docker images --format '{{.Repository}}:{{.Tag}}' | grep -i mssql | head -1 || true)
echo "img=$IMG"
# try free host tool
ls /opt/microsoft 2>/dev/null | head
# Use BE_Company with a one-off DLL query via powershell? Use node?
if command -v node >/dev/null; then echo has_node; fi

# Write a tiny console app next to runtime using shared framework
mkdir -p /tmp/sqlq && cd /tmp/sqlq
cat > Program.cs <<'CS'
using Microsoft.Data.SqlClient;
var cs = "Data Source=127.0.0.1;Initial Catalog=DatabaseCompanyNajaf;User ID=haider;Password=${SQL_PASSWORD};TrustServerCertificate=True";
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
async Task Dump(string sql) {
  await using var cmd = new SqlCommand(sql, conn);
  await using var r = await cmd.ExecuteReaderAsync();
  while (await r.ReadAsync()) {
    for (int i=0;i<r.FieldCount;i++) Console.Write($"{r.GetName(i)}={r.GetValue(i)} | ");
    Console.WriteLine();
  }
  Console.WriteLine("---");
}
await Dump("SELECT Id, FileKey, CustomerId, CustomerName, CustomerPhone, SaleId, DocumentType FROM dbo.SalesCustomerDocuments WHERE Id IN (2,3,4,5,15,16,17,18,19)");
await Dump("SELECT Id, FileKey, CustomerId, SaleId, DocumentType FROM dbo.SalesCustomerDocuments WHERE CustomerId=53327");
await Dump("SELECT SaleId, CustomerId, FullName, Phone FROM dbo.SalesDrafts WHERE CustomerId=53327 OR SaleId=2");
await Dump("SELECT SaleId, ShopImageKey FROM dbo.SalesShopProfiles WHERE SaleId IN (2,4) OR SaleId IN (SELECT SaleId FROM dbo.SalesDrafts WHERE CustomerId=53327)");
// how many docs missing files conceptually - FileKey sample
await Dump("SELECT TOP 15 Id, FileKey FROM dbo.SalesCustomerDocuments ORDER BY Id");
await Dump("SELECT TOP 10 Id, FileKey FROM dbo.SalesCustomerDocuments ORDER BY Id DESC");
CS
cat > sqlq.csproj <<'XML'
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
  </ItemGroup>
</Project>
XML
dotnet run -c Release --nologo
'''
_,o,e=c.exec_command(remote, timeout=300)
print(o.read().decode('utf-8', errors='replace')[-12000:])
err=e.read().decode('utf-8', errors='replace')
if err.strip():
    print('ERR', err[-2500:])
c.close()
