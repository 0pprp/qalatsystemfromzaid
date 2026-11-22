# Starts the lab backends (run setup-lab.ps1 first):
#   branch API  Najaf copy  http://127.0.0.1:5611
#   branch API  Babil copy  http://127.0.0.1:5612
#   gateway                 http://127.0.0.1:5680
# then creates the two legal logins if they do not exist yet. Logs go to legal-lab\data\*.log.
# Stop everything with .\legal-lab\stop-lab.ps1
param([switch]$NoBuild)

$ErrorActionPreference = 'Stop'
$root = Split-Path -Parent $PSScriptRoot
$data = Join-Path $PSScriptRoot 'data'
$secrets = Get-Content (Join-Path $data 'lab-secrets.json') -Raw | ConvertFrom-Json

& (Join-Path $PSScriptRoot 'stop-lab.ps1') | Out-Null

if (-not $NoBuild) {
    dotnet build (Join-Path $root 'BE_Company\BE_Company.csproj') -nologo -v q | Select-String 'error|Build succeeded'
    dotnet build (Join-Path $root 'BE_SalesEmployee\BE_SalesEmployee.csproj') -nologo -v q | Select-String 'error|Build succeeded'
}

$pids = @()
function Start-Api([string]$name, [string]$project, [string]$dll, [string]$url, [hashtable]$extraEnv) {
    $dir = Join-Path $root $project
    $env:ASPNETCORE_ENVIRONMENT = 'LegalLab'
    $env:ASPNETCORE_URLS = $url
    foreach ($key in $extraEnv.Keys) { Set-Item "env:$key" $extraEnv[$key] }
    $process = Start-Process dotnet -ArgumentList "`"$dll`"" -WorkingDirectory $dir -WindowStyle Hidden -PassThru `
        -RedirectStandardOutput (Join-Path $data "$name.log") -RedirectStandardError (Join-Path $data "$name.err.log")
    foreach ($key in $extraEnv.Keys) { Remove-Item "env:$key" }
    Write-Host "$name -> $url (pid $($process.Id))"
    return $process.Id
}

$conn = 'Server=.\SQLEXPRESS;Integrated Security=True;TrustServerCertificate=True;Database='
$companyDll = Join-Path $root 'BE_Company\bin\Debug\net10.0\BE_Company.dll'
$gatewayDll = Join-Path $root 'BE_SalesEmployee\bin\Debug\net8.0\BE_SalesEmployee.dll'
$pids += Start-Api 'branch-najaf' 'BE_Company' $companyDll 'http://127.0.0.1:5611' @{ ConnectionStrings__DataBaseConnection = $conn + 'LegalLab_Najaf' }
$pids += Start-Api 'branch-babil' 'BE_Company' $companyDll 'http://127.0.0.1:5612' @{ ConnectionStrings__DataBaseConnection = $conn + 'LegalLab_Babil' }
$pids += Start-Api 'gateway' 'BE_SalesEmployee' $gatewayDll 'http://127.0.0.1:5680' @{}
$pids | Set-Content (Join-Path $data 'lab.pids')
Remove-Item env:ASPNETCORE_URLS, env:ASPNETCORE_ENVIRONMENT -ErrorAction SilentlyContinue

# Wait for the gateway, then make sure the two legal logins exist.
$gw = 'http://127.0.0.1:5680/api'
$token = $null
foreach ($i in 1..40) {
    try {
        $login = Invoke-RestMethod -Method Post -Uri "$gw/Auth/LoginDirector" -ContentType 'application/json; charset=utf-8' `
            -Body (@{ userName = $secrets.DirectorUser; password = $secrets.DirectorPassword } | ConvertTo-Json)
        $token = $login.token
        break
    } catch { Start-Sleep -Milliseconds 750 }
}
if (-not $token) { throw 'Gateway did not start. See legal-lab\data\gateway.log' }

$headers = @{ Authorization = "Bearer $token" }
$existing = (Invoke-RestMethod -Uri "$gw/director/oversight/accounts" -Headers $headers) | ForEach-Object { $_.userName }
$wanted = @(
    @{ userName = $secrets.AccountantUser; password = $secrets.AccountantPassword; role = 'محاسب القانونية' },
    @{ userName = $secrets.TeamUser; password = $secrets.TeamPassword; role = 'مدير القانونية' }
)
foreach ($account in $wanted) {
    if ($existing -notcontains $account.userName) {
        $json = [System.Text.Encoding]::UTF8.GetBytes(($account | ConvertTo-Json))
        Invoke-RestMethod -Method Post -Uri "$gw/director/oversight/accounts" -Headers $headers `
            -ContentType 'application/json; charset=utf-8' -Body $json | Out-Null
        Write-Host "Created login $($account.userName) ($($account.role))"
    }
}
Write-Host 'Lab backends are up.'
