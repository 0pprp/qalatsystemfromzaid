#!/usr/bin/env python3
import os, sys
sys.stdout.reconfigure(encoding="utf-8", errors="replace")
import paramiko
c = paramiko.SSHClient()
c.set_missing_host_key_policy(paramiko.AutoAddPolicy())
c.connect("169.58.236.52", username="root", password=os.environ["SSH_PASS"], timeout=60, allow_agent=False, look_for_keys=False)

# Query SQL for docs 2-5 and sale 2 shop image keys on Najaf
sql = r"""
sqlcmd -S 127.0.0.1 -U haider -P '${SQL_PASSWORD}' -d DatabaseCompanyNajaf -h -1 -W -Q "
SET NOCOUNT ON;
SELECT Id, FileKey, ContentType, CustomerId FROM dbo.SalesCustomerDocuments WHERE Id IN (2,3,4,5);
SELECT TOP 5 Id, FileKey FROM dbo.SalesCustomerDocuments ORDER BY Id DESC;
SELECT SaleId, ShopImagePath FROM dbo.SalesShopProfiles WHERE SaleId=2;
SELECT TOP 3 SaleId, ShopImagePath FROM dbo.SalesShopProfiles ORDER BY SaleId DESC;
"
"""
_,o,e=c.exec_command(sql, timeout=60)
print(o.read().decode("utf-8", errors="replace"))
print(e.read().decode("utf-8", errors="replace")[-800:])
c.close()
