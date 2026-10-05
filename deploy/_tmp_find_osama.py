#!/usr/bin/env python3
"""Find Rusafa sales for أسامه حسين موسى."""
from __future__ import annotations

import json
import os
import sys
import textwrap

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2

    remote = textwrap.dedent(r'''
        set +e
        python3 <<'PY'
        import json, glob, os

        cs = None
        cfg = None
        for p in sorted(glob.glob('/opt/qalaa-runtime/Admin*/appsettings.json')):
            d = json.load(open(p))
            c = (d.get('ConnectionStrings') or {}).get('DataBaseConnection') or ''
            if 'BaghdadRosafa' in c or 'BaghdadRusafa' in c:
                cs, cfg = c, p
                break
        print('CFG', cfg)
        print('DB_IN_CS', 'BaghdadRosafa' in (cs or ''), 'Rusafa' in (cs or ''))
        if not cs:
            raise SystemExit('no rusafa connection')

        os.makedirs('/tmp/sql_osama', exist_ok=True)
        open('/tmp/sql_osama/sqlq.csproj', 'w').write("""<Project Sdk="Microsoft.NET.Sdk">
          <PropertyGroup>
            <OutputType>Exe</OutputType>
            <TargetFramework>net8.0</TargetFramework>
            <ImplicitUsings>enable</ImplicitUsings>
            <Nullable>enable</Nullable>
          </PropertyGroup>
          <ItemGroup>
            <PackageReference Include="Microsoft.Data.SqlClient" Version="5.2.2" />
          </ItemGroup>
        </Project>""")

        cs_lit = cs.replace('\\', '\\\\').replace('"', '\\"')
        open('/tmp/sql_osama/Program.cs', 'w').write("""
        using Microsoft.Data.SqlClient;
        var cs = \"""" + cs_lit + """\";
        await using var conn = new SqlConnection(cs);
        await conn.OpenAsync();
        Console.WriteLine("DB=" + conn.Database);

        async Task Q(string title, string sql) {
          Console.WriteLine("==== " + title);
          await using var cmd = new SqlCommand(sql, conn);
          await using var r = await cmd.ExecuteReaderAsync();
          int n=0;
          while (await r.ReadAsync()) {
            n++;
            for (int i=0;i<r.FieldCount;i++) Console.Write(r.GetName(i)+"="+r.GetValue(i)+" | ");
            Console.WriteLine();
          }
          if (n==0) Console.WriteLine("(none)");
        }

        await Q("drafts name", @"
        SELECT SaleId, CustomerId, FullName, Phone, Status, BaseSalePrice, FinalSalePrice,
         DefaultTotalSalePrice, OverrideTotalSalePrice, CompletedAt, CreatedAt, CustomerSaleId, SalesRequestId
        FROM dbo.SalesDrafts
        WHERE FullName LIKE N'%اسامه%موسى%' OR FullName LIKE N'%أسامه%موسى%'
           OR FullName LIKE N'%اسامة%موسى%' OR FullName LIKE N'%أسامة%موسى%'
           OR FullName LIKE N'%اسامه حسين%' OR FullName LIKE N'%أسامه حسين%'
           OR FullName LIKE N'%اسامة حسين%' OR FullName LIKE N'%أسامة حسين%'
        ORDER BY ISNULL(CompletedAt, CreatedAt) DESC");

        await Q("customers name", @"
        SELECT CustomerID, CustomerName, PhoneNumber, Address, DateCreate,
               AmountTotalSales, ReceiptsTotal, AmountRemaining, CountReceiptDevice
        FROM dbo.Customers
        WHERE CustomerName LIKE N'%اسامه%موسى%' OR CustomerName LIKE N'%أسامه%موسى%'
           OR CustomerName LIKE N'%اسامة%موسى%' OR CustomerName LIKE N'%أسامة%موسى%'
           OR CustomerName LIKE N'%اسامه حسين%' OR CustomerName LIKE N'%أسامه حسين%'
           OR CustomerName LIKE N'%اسامة حسين%' OR CustomerName LIKE N'%أسامة حسين%'
        ORDER BY CustomerID DESC");

        await Q("sales by amount window", @"
        SELECT cs.CustomerSaleID, cs.CustomerID, c.CustomerName, c.PhoneNumber, cs.DateCreate,
               cs.AmountTotalSalesDenar, cs.AmountTotalDenar, cs.AmountRemaining, cs.ReceiptsTotal,
               cs.ItemsNames, cs.SaleName, cs.UserName
        FROM dbo.CustomersSales cs
        LEFT JOIN dbo.Customers c ON c.CustomerID = cs.CustomerID
        WHERE (c.CustomerName LIKE N'%اسامه%' OR c.CustomerName LIKE N'%أسامه%' OR c.CustomerName LIKE N'%اسامة%' OR c.CustomerName LIKE N'%أسامة%')
           OR cs.AmountTotalSalesDenar IN (2100000, 2905000)
           OR cs.AmountTotalDenar IN (2100000, 2905000)
           OR (cs.AmountTotalSalesDenar BETWEEN 5000000 AND 5100000)
        ORDER BY cs.DateCreate DESC");
        """)
        print('program written')
        PY

        cd /tmp/sql_osama && dotnet run -c Release --nologo
    ''')

    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        _, stdout, stderr = client.exec_command(remote, timeout=300)
        print(stdout.read().decode("utf-8", errors="replace")[-20000:])
        err = stderr.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR:", err[-4000:])
        return stdout.channel.recv_exit_status()
    finally:
        client.close()


if __name__ == "__main__":
    raise SystemExit(main())
