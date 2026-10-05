#!/usr/bin/env python3
"""Inspect payments for Osama customers 136805 and 137465 on Rusafa."""
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

await Q("pay_cols", @"
SELECT COLUMN_NAME, DATA_TYPE FROM INFORMATION_SCHEMA.COLUMNS
WHERE TABLE_NAME='CustomersPayments' ORDER BY ORDINAL_POSITION");

await Q("accounts", @"
SELECT CustomerID, CustomerName, PhoneNumber, AmountTotalSales, ReceiptsTotal, AmountRemaining, CountReceiptDevice
FROM dbo.View_Customers WHERE CustomerID IN (136805, 137465)");

await Q("sales", @"
SELECT CustomerSaleID, CustomerID, DateCreate, AmountTotalSalesDenar, ReceiptsTotal, AmountRemaining, ItemsNames
FROM dbo.View_CustomersSales WHERE CustomerID IN (136805, 137465) OR CustomerSaleID IN (139382, 140039)");

await Q("payments_summary", @"
SELECT CustomerID, COUNT(1) AS Cnt, SUM(AmountDenar) AS Total,
 MIN(PaymentDate) AS FirstPay, MAX(PaymentDate) AS LastPay
FROM dbo.CustomersPayments
WHERE CustomerID IN (136805, 137465)
GROUP BY CustomerID");

await Q("payments_after_sep26", @"
SELECT CustomerPaymentID, CustomerID, PaymentDate, AmountDenar, BoundNumber, Location, UserID, CreatedDate
FROM dbo.CustomersPayments
WHERE CustomerID IN (136805, 137465)
  AND PaymentDate >= '2026-09-26'
ORDER BY PaymentDate, CustomerPaymentID");

await Q("payments_all_recent", @"
SELECT TOP 40 CustomerPaymentID, CustomerID, PaymentDate, AmountDenar, BoundNumber, Location
FROM dbo.CustomersPayments
WHERE CustomerID IN (136805, 137465)
ORDER BY PaymentDate DESC, CustomerPaymentID DESC");

await Q("payments_total_chronological_tail", @"
SELECT CustomerPaymentID, CustomerID, PaymentDate, AmountDenar,
 SUM(AmountDenar) OVER (PARTITION BY CustomerID ORDER BY PaymentDate, CustomerPaymentID) AS Running
FROM dbo.CustomersPayments
WHERE CustomerID = 136805
ORDER BY PaymentDate, CustomerPaymentID");
'''


def main() -> int:
    sys.stdout.reconfigure(encoding="utf-8", errors="replace")
    if not SSH_PASS:
        print("SSH_PASS required")
        return 2
    c = paramiko.SSHClient()
    c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
    c.connect(HOST, username="root", password=SSH_PASS, timeout=60, allow_agent=False, look_for_keys=False)
    try:
        sftp = c.open_sftp()
        with sftp.file("/tmp/split_osama/Program.cs", "w") as f:
            f.write(PROGRAM)
        sftp.close()
        cmd = (
            "export CS=$(python3 /tmp/find_rosafa_cs.py) && "
            "cd /tmp/split_osama && dotnet run -c Release --nologo --project split.csproj"
        )
        _, o, e = c.exec_command(cmd, timeout=300)
        print(o.read().decode("utf-8", errors="replace")[-30000:])
        err = e.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR:", err[-2000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
