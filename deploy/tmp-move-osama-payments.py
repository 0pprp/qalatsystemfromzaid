#!/usr/bin/env python3
"""Move post-26/9 payments for Osama from customer 136805 -> 137465 (new iPhone sale)."""
from __future__ import annotations

import os
import sys

import paramiko

HOST = "169.58.236.52"
SSH_PASS = os.environ.get("SSH_PASS", "")
DRY = os.environ.get("DRY_RUN", "1")

PROGRAM = r'''
using Microsoft.Data.SqlClient;

var cs = Environment.GetEnvironmentVariable("CS")!;
var dry = (Environment.GetEnvironmentVariable("DRY_RUN") ?? "1") != "0";
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
Console.WriteLine("DB=" + conn.Database + " dry=" + dry);

const int OldId = 136805;
const int NewId = 137465;
var cut = new DateTime(2026, 9, 26, 0, 0, 0);

async Task Q(string t, string sql)
{
    Console.WriteLine("==== " + t);
    await using var cmd = new SqlCommand(sql, conn);
    await using var r = await cmd.ExecuteReaderAsync();
    while (await r.ReadAsync())
    {
        for (var i = 0; i < r.FieldCount; i++)
            Console.Write(r.GetName(i) + "=" + r.GetValue(i) + " | ");
        Console.WriteLine();
    }
}

await Q("to_move", @"
SELECT p.CustomerPaymentID, p.CustomerID, p.PaymentDate, v.AmountDenar, p.BoundNumber, p.Location, p.UserID
FROM dbo.CustomersPayments p
LEFT JOIN dbo.View_CustomersPayments v ON v.CustomerPaymentID = p.CustomerPaymentID
WHERE p.CustomerID = 136805 AND p.PaymentDate >= '2026-09-26'
ORDER BY p.PaymentDate, p.CustomerPaymentID");

await Q("request_to_move", @"
SELECT CustomersPaymentsRequestID, CustomerID, PaymentDate, Amount, BoundNumber, CreatedDate
FROM dbo.CustomersPaymentsRequest
WHERE CustomerID = 136805 AND PaymentDate >= '2026-09-26'");

await Q("before_accounts", @"
SELECT CustomerID, AmountTotalSales, ReceiptsTotal, AmountRemaining, CountReceiptDevice
FROM dbo.View_Customers WHERE CustomerID IN (136805, 137465)");

if (dry)
{
    Console.WriteLine("DRY_RUN_OK");
    return;
}

await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
try
{
    int movedPay;
    await using (var cmd = new SqlCommand(@"
UPDATE dbo.CustomersPayments
SET CustomerID = @newId
WHERE CustomerID = @oldId AND PaymentDate >= @cut", conn, tx))
    {
        cmd.Parameters.AddWithValue("@newId", NewId);
        cmd.Parameters.AddWithValue("@oldId", OldId);
        cmd.Parameters.AddWithValue("@cut", cut);
        movedPay = await cmd.ExecuteNonQueryAsync();
    }
    Console.WriteLine("payments_moved=" + movedPay);

    int movedReq = 0;
    try
    {
        await using var cmd = new SqlCommand(@"
UPDATE dbo.CustomersPaymentsRequest
SET CustomerID = @newId
WHERE CustomerID = @oldId AND PaymentDate >= @cut", conn, tx);
        cmd.Parameters.AddWithValue("@newId", NewId);
        cmd.Parameters.AddWithValue("@oldId", OldId);
        cmd.Parameters.AddWithValue("@cut", cut);
        movedReq = await cmd.ExecuteNonQueryAsync();
    }
    catch (Exception ex)
    {
        Console.WriteLine("request_skip=" + ex.Message);
    }
    Console.WriteLine("requests_moved=" + movedReq);

    if (movedPay < 1)
        throw new Exception("expected to move at least 1 payment");

    await tx.CommitAsync();
    Console.WriteLine("PAY_MOVE_OK");
}
catch (Exception ex)
{
    await tx.RollbackAsync();
    Console.WriteLine("FAIL " + ex);
    throw;
}

await Q("after_accounts", @"
SELECT CustomerID, CustomerName, AmountTotalSales, ReceiptsTotal, AmountRemaining, CountReceiptDevice
FROM dbo.View_Customers WHERE CustomerID IN (136805, 137465)");

await Q("after_payments_new", @"
SELECT p.CustomerPaymentID, p.CustomerID, p.PaymentDate, v.AmountDenar
FROM dbo.CustomersPayments p
LEFT JOIN dbo.View_CustomersPayments v ON v.CustomerPaymentID = p.CustomerPaymentID
WHERE p.CustomerID = 137465
ORDER BY p.PaymentDate");

await Q("after_sales", @"
SELECT CustomerSaleID, CustomerID, AmountTotalSalesDenar, ReceiptsTotal, AmountRemaining, ItemsNames
FROM dbo.View_CustomersSales WHERE CustomerID IN (136805, 137465)");
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
            f"export DRY_RUN={DRY} && "
            "cd /tmp/split_osama && dotnet run -c Release --nologo --project split.csproj"
        )
        _, o, e = c.exec_command(cmd, timeout=300)
        print(o.read().decode("utf-8", errors="replace"))
        err = e.read().decode("utf-8", errors="replace")
        if err.strip():
            print("STDERR:", err[-2000:])
        return o.channel.recv_exit_status()
    finally:
        c.close()


if __name__ == "__main__":
    raise SystemExit(main())
