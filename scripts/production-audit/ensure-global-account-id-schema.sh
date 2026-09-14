#!/usr/bin/env bash
# Idempotent Production migration readiness for dbo.Users.GlobalAccountId across 17 sales DBs.
# DOES NOT run automatically. Operator must explicitly execute on a SQL host.
# Usage:
#   export SQL_SERVER='...'
#   read -s SQL_PASSWORD; export SQL_PASSWORD
#   ./ensure-global-account-id-schema.sh          # apply (idempotent)
#   ./ensure-global-account-id-schema.sh verify   # verify only
# Never prints passwords. Does not touch UserState/Password/UserType/data rows.

set -euo pipefail
umask 077

MODE="${1:-apply}"
SQL_SERVER="${SQL_SERVER:?set SQL_SERVER}"
SQL_USER="${SQL_USER:-sa}"
: "${SQL_PASSWORD:?set SQL_PASSWORD}"
# Prefer env-based password so it is not passed via -P (visible in process lists).
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

APPLY_SQL=$(cat <<'SQL'
SET XACT_ABORT ON;
BEGIN TRAN;
IF COL_LENGTH(N'dbo.Users', N'GlobalAccountId') IS NULL
BEGIN
    ALTER TABLE dbo.Users ADD GlobalAccountId UNIQUEIDENTIFIER NULL;
END
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_GlobalAccountId' AND object_id = OBJECT_ID(N'dbo.Users'))
BEGIN
    CREATE UNIQUE NONCLUSTERED INDEX IX_Users_GlobalAccountId
        ON dbo.Users(GlobalAccountId)
        WHERE GlobalAccountId IS NOT NULL;
END
COMMIT;
SQL
)

VERIFY_SQL=$(cat <<'SQL'
SELECT
  DB_NAME() AS DatabaseName,
  CASE WHEN COL_LENGTH(N'dbo.Users', N'GlobalAccountId') IS NULL THEN 0 ELSE 1 END AS ColumnExists,
  CASE WHEN COL_LENGTH(N'dbo.Users', N'GlobalAccountId') IS NULL THEN NULL
       ELSE (SELECT TYPE_NAME(system_type_id) FROM sys.columns
             WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'GlobalAccountId') END AS ColumnType,
  CASE WHEN COL_LENGTH(N'dbo.Users', N'GlobalAccountId') IS NULL THEN NULL
       ELSE (SELECT is_nullable FROM sys.columns
             WHERE object_id = OBJECT_ID(N'dbo.Users') AND name = N'GlobalAccountId') END AS IsNullable,
  CASE WHEN EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = N'IX_Users_GlobalAccountId' AND object_id = OBJECT_ID(N'dbo.Users')
  ) THEN 1 ELSE 0 END AS IndexExists;
SQL
)

pass=0
fail=0
printf 'Database\tColumnExists\tColumnType\tNullable\tIndexExists\tRESULT\n'
for db in "${DBS[@]}"; do
  if [[ "$MODE" == "apply" ]]; then
    if ! sqlcmd -S "$SQL_SERVER" -U "$SQL_USER" -C -d "$db" -Q "$APPLY_SQL" -b >/dev/null; then
      printf '%s\t-\t-\t-\t-\tFAIL_APPLY\n' "$db"
      fail=$((fail + 1))
      continue
    fi
  fi

  out=$(sqlcmd -S "$SQL_SERVER" -U "$SQL_USER" -C -d "$db" -h -1 -W -Q "$VERIFY_SQL" 2>/dev/null || true)
  # Expect: DatabaseName ColumnExists ColumnType IsNullable IndexExists
  read -r name colexists coltype nullable indexexists <<<"$(echo "$out" | awk 'NF{print; exit}')"
  if [[ "${colexists:-0}" == "1" && "${nullable:-0}" == "1" && "${indexexists:-0}" == "1" ]]; then
    printf '%s\t%s\t%s\t%s\t%s\tPASS\n' "$db" "$colexists" "$coltype" "$nullable" "$indexexists"
    pass=$((pass + 1))
  else
    printf '%s\t%s\t%s\t%s\t%s\tFAIL\n' "$db" "${colexists:-0}" "${coltype:--}" "${nullable:--}" "${indexexists:-0}"
    fail=$((fail + 1))
  fi
done

echo "SUMMARY pass=${pass} fail=${fail} total=${#DBS[@]}"
[[ "$fail" -eq 0 && "$pass" -eq 17 ]]
