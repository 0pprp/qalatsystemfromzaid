#!/usr/bin/env python3
"""Verify Osama split on Rusafa after mutation."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")

PROGRAM = r'''
using Microsoft.Data.SqlClient;
var cs = Environment.GetEnvironmentVariable("CS")!;
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
async Task Q(string t, string sql) {
  Console.WriteLine("==== "+t);
  await using var cmd = new SqlCommand(sql, conn);
  await using var r = await cmd.ExecuteReaderAsync();
  while (await r.ReadAsync()) {
    for (int i=0;i<r.FieldCount;i++) Console.Write(r.GetName(i)+"="+r.GetValue(i)+" | ");
    Console.WriteLine();
  }
}
await Q("accounts", @"
SELECT CustomerID, CustomerName, PhoneNumber, AmountTotalSales, ReceiptsTotal, AmountRemaining
FROM dbo.View_Customers WHERE CustomerID IN (136805, 137465)");
await Q("sales", @"
SELECT CustomerSaleID, CustomerID, DateCreate, AmountTotalSalesDenar, ItemsNames
FROM dbo.View_CustomersSales WHERE CustomerSaleID IN (139382, 140039) OR CustomerID IN (136805, 137465)");
await Q("draft", @"
SELECT SaleId, CustomerId, CustomerSaleId, FinalSalePrice, CompletedAt FROM dbo.SalesDrafts WHERE SaleId=33");
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/split_osama/Program.cs", "w") as f:
            f.write(PROGRAM)
        sftp.close()
        cmd = "export CS=$(python3 /tmp/find_rosafa_cs.py) && cd /tmp/split_osama && dotnet run -c Release --nologo --project split.csproj"
        _, o, e = c.exec_command(cmd, timeout=180)
        print(o.read().decode("utf-8", errors="replace"))
        err = e.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR", err[-1500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
