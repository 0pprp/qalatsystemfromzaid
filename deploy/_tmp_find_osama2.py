#!/usr/bin/env python3
from __future__ import annotations

import os
import sys
from pathlib import Path

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
if not SSH_PASS:
    raise SystemExit("SSH_PASS required")
ROOT = Path(__file__).resolve().parent


FIND_CS = r'''
import json, glob
for p in sorted(glob.glob("/opt/qalaa-runtime/Admin*/appsettings.json")):
    d = json.load(open(p))
    c = (d.get("ConnectionStrings") or {}).get("DataBaseConnection") or ""
    if "Rosafa" in c or "Rusafa" in c:
        print(p)
        print(c)
'''

PROGRAM_CS = r'''
using Microsoft.Data.SqlClient;

var cs = Environment.GetEnvironmentVariable("CS")
    ?? throw new Exception("CS missing");
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
Console.WriteLine("DB=" + conn.Database);

async Task Q(string title, string sql)
{
    Console.WriteLine("==== " + title);
    await using var cmd = new SqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    var n = 0;
    while (await r.ReadAsync())
    {
        n++;
        for (var i = 0; i < r.FieldCount; i++)
            Console.Write(r.GetName(i) + "=" + r.GetValue(i) + " | ");
        Console.WriteLine();
    }
    if (n == 0) Console.WriteLine("(none)");
}

await Q("drafts", @"
SELECT SaleId, CustomerId, FullName, Phone, Status, BaseSalePrice, FinalSalePrice,
 DefaultTotalSalePrice, OverrideTotalSalePrice, CompletedAt, CreatedAt, CustomerSaleId, SalesRequestId
FROM dbo.SalesDrafts
WHERE FullName LIKE N'%اسامه%موسى%' OR FullName LIKE N'%أسامه%موسى%'
   OR FullName LIKE N'%اسامة%موسى%' OR FullName LIKE N'%أسامة%موسى%'
   OR FullName LIKE N'%اسامه حسين%' OR FullName LIKE N'%أسامه حسين%'
   OR FullName LIKE N'%اسامة حسين%' OR FullName LIKE N'%أسامة حسين%'
ORDER BY ISNULL(CompletedAt, CreatedAt) DESC");

await Q("customers", @"
SELECT CustomerID, CustomerName, PhoneNumber, Address, DateCreate,
       AmountTotalSales, ReceiptsTotal, AmountRemaining, CountReceiptDevice
FROM dbo.Customers
WHERE CustomerName LIKE N'%اسامه%موسى%' OR CustomerName LIKE N'%أسامه%موسى%'
   OR CustomerName LIKE N'%اسامة%موسى%' OR CustomerName LIKE N'%أسامة%موسى%'
   OR CustomerName LIKE N'%اسامه حسين%' OR CustomerName LIKE N'%أسامه حسين%'
   OR CustomerName LIKE N'%اسامة حسين%' OR CustomerName LIKE N'%أسامة حسين%'
ORDER BY CustomerID DESC");

await Q("official_sales_name_or_amount", @"
SELECT cs.CustomerSaleID, cs.CustomerID, c.CustomerName, c.PhoneNumber, cs.DateCreate,
       cs.AmountTotalSalesDenar, cs.AmountTotalDenar, cs.AmountRemaining, cs.ReceiptsTotal,
       cs.ItemsNames, cs.SaleName, cs.UserName
FROM dbo.CustomersSales cs
LEFT JOIN dbo.Customers c ON c.CustomerID = cs.CustomerID
WHERE c.CustomerName LIKE N'%اسامه%' OR c.CustomerName LIKE N'%أسامه%'
   OR c.CustomerName LIKE N'%اسامة%' OR c.CustomerName LIKE N'%أسامة%'
   OR cs.AmountTotalSalesDenar IN (2100000, 2905000, 5005000)
   OR cs.AmountTotalDenar IN (2100000, 2905000, 5005000)
ORDER BY cs.DateCreate DESC");

await Q("drafts_by_amount", @"
SELECT SaleId, CustomerId, FullName, Phone, Status, FinalSalePrice, BaseSalePrice, CompletedAt, CreatedAt, CustomerSaleId
FROM dbo.SalesDrafts
WHERE FinalSalePrice IN (2100000, 2905000, 5005000)
   OR BaseSalePrice IN (2100000, 2905000, 5005000)
   OR FinalSalePrice BETWEEN 2090000 AND 2110000
   OR FinalSalePrice BETWEEN 2895000 AND 2915000
ORDER BY ISNULL(CompletedAt, CreatedAt) DESC");
'''

CSPROJ = """<Project Sdk=\"Microsoft.NET.Sdk\">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include=\"Microsoft.Data.SqlClient\" Version=\"5.2.2\" />
  </ItemGroup>
</Project>
"""


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = client.open_sftp()
        try:
            sftp.mkdir("/tmp/sql_osama")
        except OSError:
            pass
        with sftp.file("/tmp/find_cs.py", "w") as f:
            f.write(FIND_CS)
        with sftp.file("/tmp/sql_osama/Program.cs", "w") as f:
            f.write(PROGRAM_CS)
        with sftp.file("/tmp/sql_osama/sqlq.csproj", "w") as f:
            f.write(CSPROJ)
        sftp.close()

        _, stdout, stderr = client.exec_command(
            "python3 /tmp/find_cs.py", timeout=30
        )
        cs_out = stdout.read().decode("utf-8", errors="replace")
        print(cs_out)
        lines = [ln.strip() for ln in cs_out.splitlines() if ln.strip()]
        if len(lines) < 2:
            print("no cs")
            return 2
        cs = lines[1]
        cmd = f"cd /tmp/sql_osama && CS={repr(cs)}[1:-1] && export CS && dotnet run -c Release --nologo"
        # safer export
        cmd = (
            "cd /tmp/sql_osama && "
            + "export CS=$(python3 /tmp/find_cs.py | sed -n '2p') && "
            + "echo CS_OK=${#CS} && dotnet run -c Release --nologo"
        )
        _, stdout, stderr = client.exec_command(cmd, timeout=300)
        print(stdout.read().decode("utf-8", errors="replace"))
        err = stderr.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR:", err[-3000:])
        return stdout.channel.recv_exit_status()
    finally:
        client.close()


if __name__ == "__main__":
    raise SystemExit(main())
