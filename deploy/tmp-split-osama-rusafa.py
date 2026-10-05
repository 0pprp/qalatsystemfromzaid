#!/usr/bin/env python3
"""Split merged sales for أسامه حسين موسى on Rusafa (DatabaseCompanyBaghdadRosafa).

Requires SSH_PASS. Finds customer by name, if AmountTotalSales ≈ 2.1M+2.905M
or two CustomersSales under one CustomerID, moves the 26/9 (2.905M) sale onto a new Customer.
"""
from __future__ import annotations

import os
import sys
from pathlib import Path

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
var dry = (Environment.GetEnvironmentVariable("DRY_RUN") ?? "1") != "0";
await using var conn = new SqlConnection(cs);
await conn.OpenAsync();
Console.WriteLine("DB=" + conn.Database + " dry=" + dry);

await using (var list = new SqlCommand(@"
SELECT c.CustomerID, c.CustomerName, c.PhoneNumber, c.AmountTotalSales, c.ReceiptsTotal, c.AmountRemaining,
       cs.CustomerSaleID, cs.DateCreate, cs.AmountTotalSalesDenar, cs.AmountTotalDenar, cs.ReceiptsTotal AS SaleReceipts
FROM dbo.Customers c
LEFT JOIN dbo.CustomersSales cs ON cs.CustomerID = c.CustomerID
WHERE c.CustomerName LIKE N'%اسامه%موسى%' OR c.CustomerName LIKE N'%أسامه%موسى%'
   OR c.CustomerName LIKE N'%اسامة%موسى%' OR c.CustomerName LIKE N'%أسامة%موسى%'
   OR c.CustomerName LIKE N'%اسامه حسين%' OR c.CustomerName LIKE N'%أسامه حسين%'
   OR c.CustomerName LIKE N'%أسامة حسين%' OR c.CustomerName LIKE N'%اسامة حسين%'
ORDER BY c.CustomerID, cs.CustomerSaleID", conn))
await using (var r = await list.ExecuteReaderAsync())
{
    Console.WriteLine("==== current rows ====");
    while (await r.ReadAsync())
    {
        for (int i = 0; i < r.FieldCount; i++) Console.Write(r.GetName(i) + "=" + r.GetValue(i) + " | ");
        Console.WriteLine();
    }
}

await using (var drafts = new SqlCommand(@"
SELECT SaleId, CustomerId, CustomerSaleId, FullName, Phone, FinalSalePrice, Status, CompletedAt, CreatedAt
FROM dbo.SalesDrafts
WHERE FullName LIKE N'%اسامه%موسى%' OR FullName LIKE N'%أسامه%موسى%'
   OR FullName LIKE N'%اسامة%موسى%' OR FullName LIKE N'%أسامة%موسى%'
   OR FullName LIKE N'%اسامه حسين%' OR FullName LIKE N'%أسامه حسين%'
ORDER BY ISNULL(CompletedAt, CreatedAt) DESC", conn))
await using (var r = await drafts.ExecuteReaderAsync())
{
    Console.WriteLine("==== drafts ====");
    while (await r.ReadAsync())
    {
        for (int i = 0; i < r.FieldCount; i++) Console.Write(r.GetName(i) + "=" + r.GetValue(i) + " | ");
        Console.WriteLine();
    }
}

// Pick customer that has 2+ sales OR total near 5005000
int? parentId = null;
await using (var cmd = new SqlCommand(@"
SELECT TOP 1 c.CustomerID
FROM dbo.Customers c
WHERE (c.CustomerName LIKE N'%اسامه%موسى%' OR c.CustomerName LIKE N'%أسامه%موسى%'
    OR c.CustomerName LIKE N'%اسامة%موسى%' OR c.CustomerName LIKE N'%أسامة%موسى%'
    OR c.CustomerName LIKE N'%اسامه حسين%' OR c.CustomerName LIKE N'%أسامه حسين%')
  AND (
    ISNULL(c.AmountTotalSales,0) BETWEEN 4900000 AND 5200000
    OR (SELECT COUNT(1) FROM dbo.CustomersSales s WHERE s.CustomerID=c.CustomerID) >= 2
  )
ORDER BY c.CustomerID DESC", conn))
{
    parentId = (int?)await cmd.ExecuteScalarAsync();
}
Console.WriteLine("parentCustomerId=" + parentId);
if (parentId is null or <= 0)
{
    Console.WriteLine("NO_MERGED_CUSTOMER_FOUND");
    return;
}

// Newer sale ~2905000 preferably on/after 2026-09-26
int? moveSaleId = null;
decimal moveAmount = 0;
DateTime? moveDate = null;
await using (var cmd = new SqlCommand(@"
SELECT TOP 1 CustomerSaleID, ISNULL(AmountTotalSalesDenar, AmountTotalDenar) AS Amt, DateCreate
FROM dbo.CustomersSales
WHERE CustomerID=@id
  AND (
    ISNULL(AmountTotalSalesDenar, AmountTotalDenar) BETWEEN 2890000 AND 2920000
    OR DateCreate >= '2026-09-26'
  )
ORDER BY
  CASE WHEN ISNULL(AmountTotalSalesDenar, AmountTotalDenar) BETWEEN 2890000 AND 2920000 THEN 0 ELSE 1 END,
  DateCreate DESC", conn))
{
    cmd.Parameters.AddWithValue("@id", parentId.Value);
    await using var r = await cmd.ExecuteReaderAsync();
    if (await r.ReadAsync())
    {
        moveSaleId = r.GetInt32(0);
        moveAmount = r.IsDBNull(1) ? 0 : Convert.ToDecimal(r.GetValue(1));
        moveDate = r.IsDBNull(2) ? null : r.GetDateTime(2);
    }
}
Console.WriteLine($"moveSaleId={moveSaleId} amount={moveAmount} date={moveDate}");
if (moveSaleId is null)
{
    Console.WriteLine("NO_SALE_TO_MOVE");
    return;
}

if (dry)
{
    Console.WriteLine("DRY_RUN — set DRY_RUN=0 to apply");
    return;
}

await using var tx = (SqlTransaction)await conn.BeginTransactionAsync();
try
{
    // Snapshot parent customer for clone
    string name, phone, address, shop;
    await using (var cmd = new SqlCommand(@"
SELECT CustomerName, PhoneNumber, ISNULL(Address,N''), ISNULL(ShopName,N'')
FROM dbo.Customers WHERE CustomerID=@id", conn, tx))
    {
        cmd.Parameters.AddWithValue("@id", parentId.Value);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) throw new Exception("parent missing");
        name = r.GetString(0); phone = r.IsDBNull(1)?"":r.GetString(1);
        address = r.GetString(2); shop = r.GetString(3);
    }

    int newCustomerId;
    await using (var cmd = new SqlCommand(@"
INSERT INTO dbo.Customers (CustomerName, PhoneNumber, Address, ShopName, DateCreate, CustomerState)
OUTPUT INSERTED.CustomerID
VALUES (@n, @p, @a, @s, GETDATE(), 1)", conn, tx))
    {
        // Columns may differ — fallback to minimal insert via dynamic
    }

    // Safer: clone via SELECT INTO pattern with only known columns
    await using (var cmd = new SqlCommand(@"
SELECT * FROM dbo.Customers WHERE CustomerID=@id", conn, tx))
    {
        cmd.Parameters.AddWithValue("@id", parentId.Value);
    }

    // Use sp-less clone: insert name/phone then move sale
    await using (var cmd = new SqlCommand(@"
INSERT INTO dbo.Customers (CustomerName, PhoneNumber, Address, DateCreate)
OUTPUT INSERTED.CustomerID
VALUES (@n, @p, @a, GETDATE())", conn, tx))
    {
        cmd.Parameters.AddWithValue("@n", name);
        cmd.Parameters.AddWithValue("@p", (object?)phone ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@a", (object?)address ?? DBNull.Value);
        try
        {
            newCustomerId = Convert.ToInt32(await cmd.ExecuteScalarAsync());
        }
        catch
        {
            // retry with ShopName
            await using var cmd2 = new SqlCommand(@"
INSERT INTO dbo.Customers (CustomerName, PhoneNumber, Address, ShopName, DateCreate)
OUTPUT INSERTED.CustomerID
VALUES (@n, @p, @a, @s, GETDATE())", conn, tx);
            cmd2.Parameters.AddWithValue("@n", name);
            cmd2.Parameters.AddWithValue("@p", (object?)phone ?? DBNull.Value);
            cmd2.Parameters.AddWithValue("@a", (object?)address ?? DBNull.Value);
            cmd2.Parameters.AddWithValue("@s", (object?)shop ?? DBNull.Value);
            newCustomerId = Convert.ToInt32(await cmd2.ExecuteScalarAsync());
        }
    }
    Console.WriteLine("newCustomerId=" + newCustomerId);

    await using (var cmd = new SqlCommand(@"
UPDATE dbo.CustomersSales SET CustomerID=@newId WHERE CustomerSaleID=@saleId AND CustomerID=@oldId", conn, tx))
    {
        cmd.Parameters.AddWithValue("@newId", newCustomerId);
        cmd.Parameters.AddWithValue("@saleId", moveSaleId.Value);
        cmd.Parameters.AddWithValue("@oldId", parentId.Value);
        var n = await cmd.ExecuteNonQueryAsync();
        Console.WriteLine("moved sales rows=" + n);
        if (n != 1) throw new Exception("move failed");
    }

    // Move draft link if any
    await using (var cmd = new SqlCommand(@"
UPDATE dbo.SalesDrafts
SET CustomerId=@newId
WHERE CustomerSaleId=@saleId OR (CustomerId=@oldId AND FinalSalePrice BETWEEN 2890000 AND 2920000)", conn, tx))
    {
        cmd.Parameters.AddWithValue("@newId", newCustomerId);
        cmd.Parameters.AddWithValue("@saleId", moveSaleId.Value);
        cmd.Parameters.AddWithValue("@oldId", parentId.Value);
        Console.WriteLine("drafts updated=" + await cmd.ExecuteNonQueryAsync());
    }

    // Recalc totals if columns exist
    foreach (var cid in new[] { parentId.Value, newCustomerId })
    {
        await using var cmd = new SqlCommand(@"
UPDATE c SET
  AmountTotalSales = ISNULL(x.Total,0),
  AmountRemaining = ISNULL(x.Total,0) - ISNULL(c.ReceiptsTotal,0)
FROM dbo.Customers c
OUTER APPLY (
  SELECT SUM(ISNULL(AmountTotalSalesDenar, AmountTotalDenar)) AS Total
  FROM dbo.CustomersSales s WHERE s.CustomerID=c.CustomerID
) x
WHERE c.CustomerID=@id", conn, tx);
        cmd.Parameters.AddWithValue("@id", cid);
        try { Console.WriteLine("recalc "+cid+" rows="+await cmd.ExecuteNonQueryAsync()); }
        catch (Exception ex) { Console.WriteLine("recalc skip "+cid+": "+ex.Message); }
    }

    await tx.CommitAsync();
    Console.WriteLine("SPLIT_OK parent="+parentId+" new="+newCustomerId+" movedSale="+moveSaleId);
}
catch (Exception ex)
{
    await tx.RollbackAsync();
    Console.WriteLine("FAIL " + ex);
    throw;
}
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
        print("SSH_PASS required to fix Rusafa production data")
        return 2

    dry = os.environ.get("DRY_RUN", "1")
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
            f"export DRY_RUN={dry} && "
            "echo CS_LEN=${#CS} DRY=$DRY_RUN && "
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
