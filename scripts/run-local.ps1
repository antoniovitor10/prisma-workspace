param(
    [switch]$SkipBuild
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$apiProjectPath = Join-Path $workspace 'src\Detran.Kanban.Api'
$apiDllPath = Join-Path $apiProjectPath 'bin\Release\net8.0\Detran.Kanban.Api.dll'
$webPath = Join-Path $workspace 'src\Detran.Kanban.Web'
$logPath = Join-Path $workspace '.runlogs'
New-Item -ItemType Directory -Force -Path $logPath | Out-Null

if (-not (Get-Command dotnet -ErrorAction SilentlyContinue)) {
    throw '.NET 8 SDK não encontrado no PATH.'
}
if (-not (Get-Command npm.cmd -ErrorAction SilentlyContinue)) {
    throw 'npm não encontrado no PATH.'
}
if (-not (Test-NetConnection localhost -Port 1433 -InformationLevel Quiet)) {
    throw 'SQL Server não está acessível em localhost:1433.'
}

if (-not $SkipBuild) {
    dotnet build (Join-Path $workspace 'Detran.Kanban.sln') --configuration Release
    if ($LASTEXITCODE -ne 0) { throw 'Falha no build .NET.' }
    Push-Location $webPath
    try {
        if (-not (Test-Path (Join-Path $webPath 'node_modules'))) { npm.cmd ci }
        npm.cmd run build
        if ($LASTEXITCODE -ne 0) { throw 'Falha no build React.' }
    }
    finally { Pop-Location }
}

if (-not (Test-Path $apiDllPath)) {
    throw 'Assembly Release da API não encontrado. Execute o script sem -SkipBuild primeiro.'
}

function Test-LocalPort([int]$Port) {
    return Test-NetConnection localhost -Port $Port -InformationLevel Quiet
}

if (-not (Test-LocalPort 5216)) {
    $previousEnvironment = $env:ASPNETCORE_ENVIRONMENT
    $env:ASPNETCORE_ENVIRONMENT = 'Development'
    try {
        $api = Start-Process dotnet -WindowStyle Hidden -PassThru -WorkingDirectory $apiProjectPath `
            -ArgumentList @($apiDllPath,'--urls','http://localhost:5216') `
            -RedirectStandardOutput (Join-Path $logPath 'api.out.log') `
            -RedirectStandardError (Join-Path $logPath 'api.err.log')
        Set-Content -Path (Join-Path $logPath 'api.pid') -Value $api.Id
    }
    finally { $env:ASPNETCORE_ENVIRONMENT = $previousEnvironment }
}

if (-not (Test-LocalPort 5173)) {
    $previousViteApiUrl = $env:VITE_API_URL
    $env:VITE_API_URL = 'http://localhost:5216'
    try {
        $web = Start-Process npm.cmd -WindowStyle Hidden -PassThru -WorkingDirectory $webPath `
            -ArgumentList @('run','dev','--','--host','localhost','--port','5173') `
            -RedirectStandardOutput (Join-Path $logPath 'web.out.log') `
            -RedirectStandardError (Join-Path $logPath 'web.err.log')
        Set-Content -Path (Join-Path $logPath 'web.pid') -Value $web.Id
    }
    finally { $env:VITE_API_URL = $previousViteApiUrl }
}

$deadline = (Get-Date).AddSeconds(30)
while ((Get-Date) -lt $deadline -and (-not (Test-LocalPort 5216) -or -not (Test-LocalPort 5173))) {
    Start-Sleep -Milliseconds 500
}

if (-not (Test-LocalPort 5216)) {
    throw "A API não iniciou. Consulte $logPath\api.err.log."
}
if (-not (Test-LocalPort 5173)) {
    throw "O frontend não iniciou. Consulte $logPath\web.err.log."
}

Write-Host 'Frontend: http://localhost:5173'
Write-Host 'Swagger:  http://localhost:5216/swagger'
Write-Host 'Health:   http://localhost:5216/health'
