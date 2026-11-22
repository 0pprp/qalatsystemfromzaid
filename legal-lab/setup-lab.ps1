# Builds an isolated lab for the legal system on this PC:
#   * LegalLab_Najaf / LegalLab_Babil  = copies of two local branch databases (originals are only read)
#   * LegalLab_Legal                   = empty database for the legal tables
#   * appsettings.LegalLab.json for BE_Company and BE_SalesEmployee, with lab-only random keys/passwords
# Re-run with -Fresh to drop the lab databases and start again from clean copies.
param(
    [string]$Server = '.\SQLEXPRESS',
    [string]$NajafSource = 'DatabaseCompanyNajaf',
    [string]$BabilSource = 'DatabaseCompanyBabil',
    [switch]$Fresh
)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$data = Join-Path $PSScriptRoot 'data'
New-Item -ItemType Directory -Force $data | Out-Null

function Sql([string]$query, [string]$db = 'master') {
    $out = sqlcmd -S $Server -E -C -b -W -h -1 -f 65001 -d $db -Q "SET NOCOUNT ON; $query"
    if ($LASTEXITCODE -ne 0) { throw "SQL failed: $out" }
    return $out
}

function Copy-Database([string]$source, [string]$target) {
    $exists = Sql "SELECT COUNT(*) FROM sys.databases WHERE name = N'$target'"
    if ([int]$exists -gt 0) {
        if (-not $Fresh) { Write-Host "$target already exists (use -Fresh to rebuild)"; return }
        Write-Host "Dropping $target"
        Sql "ALTER DATABASE [$target] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [$target];" | Out-Null
    }
    $backupDir = (Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultBackupPath') AS nvarchar(400))").Trim()
    $dataDir = (Sql "SELECT CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS nvarchar(400))").Trim()
    $bak = Join-Path $backupDir "$target.bak"
    Write-Host "Copying $source -> $target"
    Sql "BACKUP DATABASE [$source] TO DISK = N'$bak' WITH COPY_ONLY, INIT;" | Out-Null
    $files = sqlcmd -S $Server -E -C -b -W -h -1 -s '|' -f 65001 -Q "SET NOCOUNT ON; RESTORE FILELISTONLY FROM DISK = N'$bak';"
    $moves = @()
    foreach ($line in $files) {
        $parts = $line -split '\|'
        if ($parts.Count -lt 3) { continue }
        $logical = $parts[0].Trim(); $type = $parts[2].Trim()
        $ext = if ($type -eq 'L') { '_log.ldf' } else { '.mdf' }
        $moves += "MOVE N'$logical' TO N'$(Join-Path $dataDir ($target + $ext))'"
    }
    Sql "RESTORE DATABASE [$target] FROM DISK = N'$bak' WITH $($moves -join ', '), RECOVERY;" | Out-Null
    Sql "ALTER DATABASE [$target] SET RECOVERY SIMPLE;" | Out-Null
    Sql "DBCC SHRINKFILE (2, 64) WITH NO_INFOMSGS;" $target | Out-Null
    Remove-Item $bak -ErrorAction SilentlyContinue
}

Copy-Database $NajafSource 'LegalLab_Najaf'
Copy-Database $BabilSource 'LegalLab_Babil'

$legalExists = Sql "SELECT COUNT(*) FROM sys.databases WHERE name = N'LegalLab_Legal'"
if ([int]$legalExists -gt 0 -and $Fresh) {
    Sql "ALTER DATABASE [LegalLab_Legal] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [LegalLab_Legal];" | Out-Null
    $legalExists = 0
}
if ([int]$legalExists -eq 0) { Sql "CREATE DATABASE [LegalLab_Legal];" | Out-Null; Write-Host 'Created LegalLab_Legal' }

function New-Secret([int]$bytes) {
    $buffer = New-Object byte[] $bytes
    [System.Security.Cryptography.RandomNumberGenerator]::Create().GetBytes($buffer)
    return [Convert]::ToBase64String($buffer)
}
function New-Password { return (New-Secret 9) -replace '[+/=]', 'x' }

$secretsPath = Join-Path $data 'lab-secrets.json'
if ((Test-Path $secretsPath) -and -not $Fresh) {
    $secrets = Get-Content $secretsPath -Raw | ConvertFrom-Json
} else {
    $secrets = [pscustomobject]@{
        GatewayKey       = New-Secret 24
        BranchSigningKey = New-Secret 32
        GatewayJwtKey    = New-Secret 48
        DirectorUser     = 'lab-director'
        DirectorPassword = New-Password
        AccountantUser   = 'lab-legal-accountant'
        AccountantPassword = New-Password
        TeamUser         = 'lab-legal-team'
        TeamPassword     = New-Password
    }
    $secrets | ConvertTo-Json | Set-Content -Encoding utf8 $secretsPath
    Remove-Item (Join-Path $data 'oversight-accounts.json') -ErrorAction SilentlyContinue
}

$conn = "Server=$Server;Integrated Security=True;TrustServerCertificate=True;Database="

$company = [ordered]@{
    Logging = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning' } }
    AllowedHosts = '*'
    ConnectionStrings = @{ DataBaseConnection = $conn + 'LegalLab_Najaf' }
    SalesEmployee = @{ GatewayKey = $secrets.GatewayKey }
    SalesManagement = @{ RequireDemoDatabase = $false; BranchId = 'lab'; BranchName = 'مختبر القانونية' }
    Legal = @{ HideFromNonLegalUsers = $true }
    Authentication = @{ Schemes = @{ Bearer = @{ SigningKeys = @(@{ Issuer = 'our-app'; Value = $secrets.BranchSigningKey }) } } }
}
$company | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $root 'BE_Company\appsettings.LegalLab.json')

$gateway = [ordered]@{
    Logging = @{ LogLevel = @{ Default = 'Information'; 'Microsoft.AspNetCore' = 'Warning' } }
    AllowedHosts = '*'
    ConnectionStrings = @{ Legal = $conn + 'LegalLab_Legal' }
    InternalApiKey = $secrets.GatewayKey
    Jwt = @{ Key = $secrets.GatewayJwtKey }
    SalesManagement = @{ RequireDemoDatabase = $false }
    OversightAccounts = @{ File = (Join-Path $data 'oversight-accounts.json') }
    DirectorAccount = @{ UserName = $secrets.DirectorUser; DisplayName = 'مدير المختبر'; Password = $secrets.DirectorPassword }
    LabCities = @(
        @{ Name = 'النجف (مختبر)'; Value = 'lab-najaf'; Database = 'LegalLab_Najaf'; Link = 'http://127.0.0.1:5611/api/' },
        @{ Name = 'بابل (مختبر)'; Value = 'lab-babil'; Database = 'LegalLab_Babil'; Link = 'http://127.0.0.1:5612/api/' }
    )
}
$gateway | ConvertTo-Json -Depth 8 | Set-Content -Encoding utf8 (Join-Path $root 'BE_SalesEmployee\appsettings.LegalLab.json')

Write-Host ''
Write-Host 'Lab is ready. Logins are in legal-lab\data\lab-secrets.json. Next: .\legal-lab\start-lab.ps1'
