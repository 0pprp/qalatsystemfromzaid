#!/usr/bin/env python3
"""Split Osama sale 140039 (26/9 2.905M) off customer 136805 onto a new CustomerID."""
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

const int OldCustomerId = 136805;
const int MoveSaleId = 140039; // 26/9 new sale
const int KeepSaleId = 139382; // prior sale ~2.1M
const int DraftSaleId = 33;

async Task Q(string title, string sql, params (string, object)[] ps)
{
    Console.WriteLine("==== " + title);
    await using var cmd = new SqlCommand(sql, conn);
    foreach (var (n, v) in ps) cmd.Parameters.AddWithValue(n, v ?? DBNull.Value);
    await using var r = await cmd.ExecuteReaderAsync();
    var n0 = 0;
    while (await r.ReadAsync())
    {
        n0++;
        for (var i = 0; i < r.FieldCount; i++)
            Console.Write(r.GetName(i) + "=" + r.GetValue(i) + " | ");
        Console.WriteLine();
    }
    if (n0 == 0) Console.WriteLine("(none)");
}

await Q("amounts_via_view", @"
SELECT v.CustomerSaleID, v.CustomerID, v.DateCreate, v.AmountTotalSalesDenar, v.AmountDaySalesDenar, v.ReceiptsTotal, v.AmountRemaining, v.ItemsNames
FROM dbo.View_CustomersSales v
WHERE v.CustomerID = @cid OR v.CustomerSaleID IN (@a,@b)
ORDER BY v.CustomerSaleID",
    ("@cid", OldCustomerId), ("@a", MoveSaleId), ("@b", KeepSaleId));

await Q("customer_account", @"
SELECT CustomerID, CustomerName, PhoneNumber, AmountTotalSales, ReceiptsTotal, AmountRemaining
FROM dbo.View_Customers
WHERE CustomerID = @cid", ("@cid", OldCustomerId));

await Q("select_items_cols", @"
SELECT COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_NAME='SelectItemsSales' ORDER BY ORDINAL_POSITION");

await Q("items_for_sales", @"
SELECT * FROM dbo.SelectItemsSales WHERE CustomerSaleID IN (@a,@b)",
    ("@a", MoveSaleId), ("@b", KeepSaleId));

await Q("draft", @"
SELECT SaleId, CustomerId, CustomerSaleId, FullName, Phone, FinalSalePrice, Status, CompletedAt
FROM dbo.SalesDrafts WHERE SaleId=@id OR CustomerSaleId=@a",
    ("@id", DraftSaleId), ("@a", MoveSaleId));

if (dry)
{
    Console.WriteLine("DRY_RUN_OK — will move CustomerSaleID=" + MoveSaleId + " off CustomerID=" + OldCustomerId);
    return;
}

await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
try
{
    // Verify preconditions
    await using (var chk = new SqlCommand(@"
SELECT COUNT(1) FROM dbo.CustomersSales WHERE CustomerID=@c AND CustomerSaleID=@s", conn, tx))
    {
        chk.Parameters.AddWithValue("@c", OldCustomerId);
        chk.Parameters.AddWithValue("@s", MoveSaleId);
        var ok = Convert.ToInt32(await chk.ExecuteScalarAsync());
        if (ok != 1) throw new Exception("move sale not on expected customer");
    }

    string name, phone, address, shop, notes, nearest, saleName, receiptName;
    int? delegateId, userId, cityId;
    await using (var cmd = new SqlCommand(@"
SELECT CustomerName, PhoneNumber, ISNULL(Address,N''), ISNULL(ShopName,N''), ISNULL(Notes,N''),
       ISNULL(NearestFunctionPoint,N''), ISNULL(SaleName,N''), ISNULL(ReceiptName,N''),
       DelegateID, UserID, CityID
FROM dbo.Customers WHERE CustomerID=@id", conn, tx))
    {
        cmd.Parameters.AddWithValue("@id", OldCustomerId);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) throw new Exception("customer missing");
        name = r.GetString(0);
        phone = r.IsDBNull(1) ? "" : r.GetString(1);
        address = r.GetString(2);
        shop = r.GetString(3);
        notes = r.GetString(4);
        nearest = r.GetString(5);
        saleName = r.GetString(6);
        receiptName = r.GetString(7);
        delegateId = r.IsDBNull(8) ? null : r.GetInt32(8);
        userId = r.IsDBNull(9) ? null : r.GetInt32(9);
        cityId = r.IsDBNull(10) ? null : r.GetInt32(10);
    }

    int newCustomerId;
    await using (var cmd = new SqlCommand(@"
INSERT INTO dbo.Customers
(DelegateID, UserID, CityID, CustomerName, Address, Notes, PhoneNumber, CustomerState, ShopName,
 NearestFunctionPoint, SaleName, ReceiptName, AsyncState, AsyncID, SelectState, IsLegal, CreatedDate, IsFakeSale)
OUTPUT INSERTED.CustomerID
VALUES
(@DelegateID, @UserID, @CityID, @CustomerName, @Address, @Notes, @PhoneNumber, 1, @ShopName,
 @NearestFunctionPoint, @SaleName, @ReceiptName, 0, UPPER(CONVERT(nvarchar(36), NEWID())), 0, 0, GETDATE(), 0)", conn, tx))
    {
        cmd.Parameters.AddWithValue("@DelegateID", (object?)delegateId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UserID", (object?)userId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CityID", (object?)cityId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CustomerName", name.Trim());
        cmd.Parameters.AddWithValue("@Address", address);
        cmd.Parameters.AddWithValue("@Notes", notes);
        cmd.Parameters.AddWithValue("@PhoneNumber", phone);
        cmd.Parameters.AddWithValue("@ShopName", shop);
        cmd.Parameters.AddWithValue("@NearestFunctionPoint", nearest);
        cmd.Parameters.AddWithValue("@SaleName", saleName);
        cmd.Parameters.AddWithValue("@ReceiptName", receiptName);
        newCustomerId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
    }
    Console.WriteLine("newCustomerId=" + newCustomerId);

    await using (var cmd = new SqlCommand(@"
UPDATE dbo.CustomersSales
SET CustomerID=@newId
WHERE CustomerSaleID=@saleId AND CustomerID=@oldId", conn, tx))
    {
        cmd.Parameters.AddWithValue("@newId", newCustomerId);
        cmd.Parameters.AddWithValue("@saleId", MoveSaleId);
        cmd.Parameters.AddWithValue("@oldId", OldCustomerId);
        var n = await cmd.ExecuteNonQueryAsync();
        if (n != 1) throw new Exception("failed moving CustomersSales row n=" + n);
    }

    await using (var cmd = new SqlCommand(@"
UPDATE dbo.SalesDrafts
SET CustomerId=@newId
WHERE SaleId=@draftId AND CustomerSaleId=@saleId", conn, tx))
    {
        cmd.Parameters.AddWithValue("@newId", newCustomerId);
        cmd.Parameters.AddWithValue("@draftId", DraftSaleId);
        cmd.Parameters.AddWithValue("@saleId", MoveSaleId);
        Console.WriteLine("drafts_updated=" + await cmd.ExecuteNonQueryAsync());
    }

    // If any payments were wrongly on old customer after 26/9 for the new sale only — leave shared payments on old.
    // Payments table is per CustomerID not per CustomerSale typically.

    await using (var cmd = new SqlCommand(@"
UPDATE dbo.SalesRequests
SET ExistingCustomerId=@newId
WHERE ExistingCustomerId=@oldId
  AND (
    ConvertedToSaleId=@draftId
    OR (CustomerPhone=@phone AND CreatedAtUtc >= '2026-09-26')
  )", conn, tx))
    {
        cmd.Parameters.AddWithValue("@newId", newCustomerId);
        cmd.Parameters.AddWithValue("@oldId", OldCustomerId);
        cmd.Parameters.AddWithValue("@draftId", DraftSaleId);
        cmd.Parameters.AddWithValue("@phone", phone);
        try { Console.WriteLine("requests_updated=" + await cmd.ExecuteNonQueryAsync()); }
        catch (Exception ex) { Console.WriteLine("requests_skip=" + ex.Message); }
    }

    await tx.CommitAsync();
    Console.WriteLine($"SPLIT_OK old={OldCustomerId} keepSale={KeepSaleId} new={newCustomerId} movedSale={MoveSaleId}");
}
catch (Exception ex)
{
    await tx.RollbackAsync();
    Console.WriteLine("FAIL " + ex);
    throw;
}

// verify
await using (var cmd = new SqlCommand(@"
SELECT CustomerSaleID, CustomerID, DateCreate FROM dbo.CustomersSales WHERE CustomerSaleID IN (@a,@b)
UNION ALL
SELECT SaleId, CustomerId, CompletedAt FROM dbo.SalesDrafts WHERE SaleId=@d", conn))
{
    cmd.Parameters.AddWithValue("@a", MoveSaleId);
    cmd.Parameters.AddWithValue("@b", KeepSaleId);
    cmd.Parameters.AddWithValue("@d", DraftSaleId);
    await using var r = await cmd.ExecuteReaderAsync();
    Console.WriteLine("==== after");
    while (await r.ReadAsync())
        Console.WriteLine($"{r.GetValue(0)} -> customer/sale {r.GetValue(1)} at {r.GetValue(2)}");
}

await Q("account_old", @"
SELECT CustomerID, CustomerName, AmountTotalSales, ReceiptsTotal, AmountRemaining
FROM dbo.View_Customers WHERE CustomerID=@cid", ("@cid", OldCustomerId));
'''


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
        with sftp.file("/tmp/split_osama/Program.cs", "w") as f:
            f.write(PROGRAM)
        sftp.close()
        cmd = (
            "export CS=$(python3 /tmp/find_rosafa_cs.py) && "
            f"export DRY_RUN={DRY} && "
            "echo DRY=$DRY_RUN && "
            "cd /tmp/split_osama && dotnet run -c Release --nologo --project split.csproj"
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
