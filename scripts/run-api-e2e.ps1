# Inicia a API .NET usando o banco de dados E2E dedicado
# Uso: .\scripts\run-api-e2e.ps1

param(
    [string]$Urls = "http://127.0.0.1:5400"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "Test-E2EDatabaseName.ps1")

# Lê a connection string do arquivo
if (-not (Test-Path ".env.e2e.connection")) {
    throw "Arquivo .env.e2e.connection não encontrado. Execute primeiro: .\scripts\setup-e2e-database.ps1"
}

$e2eConn = (Get-Content ".env.e2e.connection" -Raw).Trim()
$databaseMatch = [regex]::Match(
    $e2eConn,
    "(?:Initial Catalog|Database)\s*=\s*([^;]+)",
    [System.Text.RegularExpressions.RegexOptions]::IgnoreCase
)

if (-not $databaseMatch.Success) {
    throw "Não foi possível extrair o nome do banco do arquivo E2E."
}

$DatabaseName = $databaseMatch.Groups[1].Value.Trim()
Test-E2EDatabaseName -DatabaseName $DatabaseName | Out-Null

# Lê a senha seed
if (-not (Test-Path ".env.e2e.seedpassword")) {
    throw "Arquivo .env.e2e.seedpassword não encontrado. Execute primeiro: .\scripts\setup-e2e-database.ps1"
}

$seedPassword = (Get-Content ".env.e2e.seedpassword" -Raw).Trim()

# Configura variáveis de ambiente
$env:ConnectionStrings__DefaultConnection = $e2eConn
$env:Seed__DemoPassword = $seedPassword
$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:ASPNETCORE_URLS = $Urls

# A configuração Development registra cada comando SQL. Durante o E2E isso gera
# centenas de milhares de linhas e pode atrasar/cancelar requisições concorrentes.
$env:Serilog__MinimumLevel__Default = "Warning"
$env:Serilog__MinimumLevel__Override__Microsoft = "Warning"
[Environment]::SetEnvironmentVariable(
    "Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command",
    "Warning",
    "Process"
)

Write-Host "API E2E iniciando para banco aprovado: $DatabaseName na URL $Urls" -ForegroundColor Cyan

dotnet run --project src/Detran.Kanban.Api --no-launch-profile
