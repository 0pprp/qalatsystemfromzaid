#!/usr/bin/env bash
# Read-only Production audit for Main Accountant + Sales Manager across branch DBs.
# Usage (on SQL-capable host):
#   export SQL_SERVER='...'
#   read -s SQL_PASSWORD; export SQL_PASSWORD
#   ./global-users-audit.sh
# Never prints Password / PasswordHash / JWT.

set -euo pipefail
umask 077

SQL_SERVER="${SQL_SERVER:?set SQL_SERVER}"
SQL_USER="${SQL_USER:-sa}"
: "${SQL_PASSWORD:?set SQL_PASSWORD}"
export SQLCMDPASSWORD="$SQL_PASSWORD"
unset SQL_PASSWORD

DBS=(
  DatabaseCompanyDewania
  DatabaseCompanyKarbala
  DatabaseCompanyKot
  DatabaseCompanyMaysan
  DatabaseCompanyMothana
  DatabaseCompanyMusol
  DatabaseCompanyNajaf
  DatabaseCompanyNasria
  DatabaseCompanyBabil
  DatabaseCompanyBasra
  DatabaseCompanyBasraAlanwar
  DatabaseCompanyDeiala
  DatabaseCompanyBaghdadKarak
  DatabaseCompanyKarkok
  DatabaseCompanyBaghdadRosafa
  DatabaseCompanyKarakAqeel
  DatabaseCompanyRusafaAqeel
)

QUERY=$(cat <<'SQL'
SELECT
  DB_NAME() AS DatabaseName,
  UserID,
  UserName,
  UserType,
  UserState,
  AsyncState,
  CASE WHEN COL_LENGTH('dbo.Users','GlobalAccountId') IS NULL THEN NULL ELSE CAST(GlobalAccountId AS nvarchar(36)) END AS GlobalAccountId,
  COUNT(*) OVER (PARTITION BY LOWER(LTRIM(RTRIM(UserName)))) AS DuplicateCount
FROM dbo.Users
WHERE UserType IN (N'محاسب رئيسي', N'مدير مبيعات')
ORDER BY UserType, UserName, UserID;
SQL
)

for db in "${DBS[@]}"; do
  echo "===== ${db} ====="
  sqlcmd -S "$SQL_SERVER" -U "$SQL_USER" -C -d "$db" -Q "$QUERY" -W -s "|" || {
    echo "BRANCH_UNAVAILABLE ${db}"
  }
done
