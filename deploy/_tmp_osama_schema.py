#!/usr/bin/env python3
"""Inspect Rusafa Customers/CustomersSales schema and find Osama sales."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

FIND_CS = r'''
import json, glob
for p in sorted(glob.glob("/opt/qalaa-runtime/Admin*/appsettings.json")):
    d = json.load(open(p))
    c = (d.get("ConnectionStrings") or {}).get("DataBaseConnection") or ""
    if "BaghdadRosafa" in c:
        print(c)
        break
'''

PROGRAM = r'''
using Microsoft.Data.SqlClient;

var cs = Environment.GetEnvironmentVariable("CS")!;
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

await Q("customers_cols", @"
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='Customers'
ORDER BY ORDINAL_POSITION");

await Q("sales_cols", @"
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='CustomersSales'
ORDER BY ORDINAL_POSITION");

await Q("drafts_cols_sale", @"
SELECT COLUMN_NAME, DATA_TYPE
FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_SCHEMA='dbo' AND TABLE_NAME='SalesDrafts'
  AND COLUMN_NAME LIKE '%Customer%' OR (TABLE_NAME='SalesDrafts' AND COLUMN_NAME IN ('SaleId','FinalSalePrice','CompletedAt','FullName','Phone','Status'))
ORDER BY ORDINAL_POSITION");

await Q("customers_osama", @"
SELECT TOP 20 *
FROM dbo.Customers
WHERE CustomerName LIKE N'%اسامه%موسى%' OR CustomerName LIKE N'%أسامه%موسى%'
   OR CustomerName LIKE N'%اسامة%موسى%' OR CustomerName LIKE N'%أسامة%موسى%'
   OR CustomerName LIKE N'%اسامه حسين%' OR CustomerName LIKE N'%أسامه حسين%'
   OR CustomerName LIKE N'%أسامة حسين%' OR CustomerName LIKE N'%اسامة حسين%'
ORDER BY CustomerID DESC");

await Q("drafts_osama", @"
SELECT SaleId, CustomerId, FullName, Phone, Status, FinalSalePrice, BaseSalePrice, CompletedAt, CreatedAt, CustomerSaleId
FROM dbo.SalesDrafts
WHERE FullName LIKE N'%اسامه%موسى%' OR FullName LIKE N'%أسامه%موسى%'
   OR FullName LIKE N'%اسامة%موسى%' OR FullName LIKE N'%أسامة%موسى%'
   OR FullName LIKE N'%اسامه حسين%' OR FullName LIKE N'%أسامه حسين%'
   OR FullName LIKE N'%أسامة حسين%' OR FullName LIKE N'%اسامة حسين%'
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
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = client.open_sftp()
        try:
            sftp.mkdir("/tmp/split_osama")
        except OSError:
            pass
        with sftp.file("/tmp/find_rosafa_cs.py", "w") as f:
            f.write(FIND_CS)
        with sftp.file("/tmp/split_osama/Program.cs", "w") as f:
            f.write(PROGRAM)
        with sftp.file("/tmp/split_osama/split.csproj", "w") as f:
            f.write(CSPROJ)
        sftp.close()

        cmd = (
            "export CS=$(python3 /tmp/find_rosafa_cs.py) && "
            "cd /tmp/split_osama && dotnet run -c Release --nologo --project split.csproj"
        )
        _, stdout, stderr = client.exec_command(cmd, timeout=300)
        out = stdout.read().decode("utf-8", errors="replace")
        err = stderr.read().decode("utf-8", errors="replace")
        print(out[-20000:])
        if err.strip():
            print("STDERR:", err[-3000:])
        return stdout.channel.recv_exit_status()
    finally:
        client.close()


if __name__ == "__main__":
    raise SystemExit(main())
