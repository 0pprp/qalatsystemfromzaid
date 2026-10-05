#!/usr/bin/env python3
"""Deep inspect Osama 136805/136029 sales and amounts on Rusafa."""
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

await Q("sales_for_customers", @"
SELECT * FROM dbo.CustomersSales
WHERE CustomerID IN (136805, 136029)
ORDER BY CustomerID, CustomerSaleID");

await Q("select_items_sales", @"
SELECT TOP 50 *
FROM dbo.SelectItemsSales
WHERE CustomerSaleID IN (
  SELECT CustomerSaleID FROM dbo.CustomersSales WHERE CustomerID IN (136805, 136029)
)
OR CustomerID IN (136805, 136029)");

// discover amount-related objects
await Q("views_amount", @"
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.VIEWS
WHERE TABLE_NAME LIKE '%Customer%' OR TABLE_NAME LIKE '%Sale%'
ORDER BY TABLE_NAME");

await Q("cols_selectitemssales", @"
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='SelectItemsSales' ORDER BY ORDINAL_POSITION");

await Q("drafts_phone", @"
SELECT SaleId, CustomerId, CustomerSaleId, FullName, Phone, Status, FinalSalePrice, CompletedAt, CreatedAt
FROM dbo.SalesDrafts
WHERE Phone = N'07700161516' OR CustomerId IN (136805, 136029)
ORDER BY ISNULL(CompletedAt, CreatedAt) DESC");

await Q("payments", @"
SELECT TOP 30 *
FROM dbo.CustomersPayments
WHERE CustomerID IN (136805, 136029)
ORDER BY CustomerPaymentID DESC");

await Q("try_get_by_id_view", @"
SELECT name FROM sys.objects WHERE type IN ('P','FN','TF','IF','V') AND name LIKE '%Customer%Get%'
ORDER BY name");
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    client = paramiko.SSHClient()
    client.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    client.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = client.open_sftp()
        with sftp.file("/tmp/split_osama/Program.cs", "w") as f:
            f.write(PROGRAM)
        sftp.close()
        cmd = (
            "export CS=$(python3 /tmp/find_rosafa_cs.py) && "
            "cd /tmp/split_osama && dotnet run -c Release --nologo --project split.csproj"
        )
        _, stdout, stderr = client.exec_command(cmd, timeout=300)
        print(stdout.read().decode("utf-8", errors="replace")[-25000:])
        err = stderr.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR:", err[-2500:])
        return stdout.channel.recv_exit_status()
    finally:
        client.close()


if __name__ == "__main__":
    raise SystemExit(main())
