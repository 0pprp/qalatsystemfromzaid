#!/usr/bin/env python3
"""Find how payment amounts are stored for Osama on Rusafa."""
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

async Task Q(string t, string sql)
{
    Console.WriteLine("==== " + t);
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

await Q("tables_pay", @"
SELECT TABLE_NAME FROM INFORMATION_SCHEMA.TABLES
WHERE TABLE_NAME LIKE '%Payment%' OR TABLE_NAME LIKE '%Receipt%'
ORDER BY TABLE_NAME");

await Q("view_pay_def", @"
SELECT OBJECT_DEFINITION(OBJECT_ID('dbo.View_CustomersPayments'))");

await Q("pay_count", @"
SELECT CustomerID, COUNT(1) Cnt
FROM dbo.CustomersPayments WHERE CustomerID IN (136805,137465)
GROUP BY CustomerID");

await Q("pay_rows_sample", @"
SELECT TOP 5 * FROM dbo.CustomersPayments WHERE CustomerID=136805 ORDER BY CustomerPaymentID DESC");

await Q("pay_after_sep26_count", @"
SELECT COUNT(1) FROM dbo.CustomersPayments
WHERE CustomerID=136805 AND PaymentDate >= '2026-09-26'");

await Q("pay_after_sep26", @"
SELECT CustomerPaymentID, CustomerID, PaymentDate, BoundNumber, Location, UserID, CreatedDate, AsyncID
FROM dbo.CustomersPayments
WHERE CustomerID IN (136805,137465) AND PaymentDate >= '2026-09-26'
ORDER BY PaymentDate, CustomerPaymentID");

// Try common amount tables
await Q("request_cols", @"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='CustomersPaymentsRequest' ORDER BY ORDINAL_POSITION");

await Q("try_view_receipts", @"
SELECT TOP 20 * FROM dbo.View_CustomersPayments WHERE CustomerID IN (136805,137465) ORDER BY PaymentDate DESC");
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
        _, o, e = c.exec_command(cmd, timeout=300)
        print(o.read().decode("utf-8", errors="replace")[-35000:])
        err = e.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR:", err[-2500:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
