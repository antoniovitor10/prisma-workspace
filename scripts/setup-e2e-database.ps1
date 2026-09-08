# Setup completo do banco de dados para testes E2E
# Uso: .\scripts\setup-e2e-database.ps1

param(
    [string]$SqlServer = "localhost",
    [string]$SqlUser = "sa",
    [string]$SqlPassword = "",
    [string]$DatabaseName = "DetranKanban_E2E",
    [string]$SeedPassword = "",
    [string]$TestUserEmail = "admin@prisma.example.invalid"
)

$ErrorActionPreference = "Stop"

. (Join-Path $PSScriptRoot "Test-E2EDatabaseName.ps1")
Test-E2EDatabaseName -DatabaseName $DatabaseName | Out-Null

if ($DatabaseName -ne "DetranKanban_E2E") {
    throw "Banco recusado para setup: '$DatabaseName'. Este setup suporta somente DetranKanban_E2E; use o fluxo exclusivo para nomes DetranKanban_MigrationTest_."
}

Write-Host "=== Setup do Banco E2E ===" -ForegroundColor Cyan

# Valida parâmetros
if ([string]::IsNullOrWhiteSpace($SqlPassword)) {
    $SqlPassword = Read-Host "Digite a senha do SA (ou usuário SQL):" -AsSecureString
    $SqlPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($SqlPassword)
    )
}

# Connection string para o banco E2E
$e2eConn = "Server=$SqlServer;Database=$DatabaseName;User Id=$SqlUser;Password=$SqlPassword;TrustServerCertificate=True;"
$env:ConnectionStrings__DefaultConnection = $e2eConn
$env:Jwt__Key = "Prisma-E2E-only-signing-key-2026-at-least-32-bytes"
$env:Setup__Enabled = "false"

Write-Host "`n[1/4] Preparando banco de dados $DatabaseName..." -ForegroundColor Yellow
if (Get-Command sqlcmd -ErrorAction SilentlyContinue) {
    try {
        sqlcmd -S $SqlServer -U $SqlUser -P $SqlPassword -i "scripts\e2e-create-database.sql" -v DatabaseName=$DatabaseName
        Write-Host "Banco criado com sucesso." -ForegroundColor Green
    } catch {
        Write-Host "Erro ao criar banco: $_" -ForegroundColor Red
        exit 1
    }
} else {
    Write-Host "sqlcmd não encontrado; removendo o banco E2E pelo EF Core antes de recriá-lo." -ForegroundColor Yellow
    dotnet ef database drop --force --project src/Prisma.Workspace.Infrastructure --startup-project src/Prisma.Workspace.Api
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database drop terminou com código $LASTEXITCODE."
    }
}

Write-Host "`n[2/4] Executando migrations do EF Core..." -ForegroundColor Yellow
try {
    dotnet ef database update --project src/Prisma.Workspace.Infrastructure --startup-project src/Prisma.Workspace.Api
    if ($LASTEXITCODE -ne 0) {
        throw "dotnet ef database update terminou com código $LASTEXITCODE."
    }
    Write-Host "Migrations aplicadas com sucesso." -ForegroundColor Green
} catch {
    Write-Host "Erro ao aplicar migrations: $_" -ForegroundColor Red
    exit 1
}

Write-Host "`n[3/4] Configurando User Secrets para API usar banco E2E..." -ForegroundColor Yellow
try {
    # Salva a connection string do E2E em um arquivo separado (não versionado)
    $e2eConn | Out-File -FilePath ".env.e2e.connection" -Encoding UTF8 -Force
    Write-Host "Connection string salva em .env.e2e.connection (gitignored)." -ForegroundColor Green
    Write-Host "Para rodar a API com banco E2E, use: .\scripts\run-api-e2e.ps1" -ForegroundColor Yellow
} catch {
    Write-Host "Aviso: não foi possível salvar .env.e2e.connection: $_" -ForegroundColor Yellow
}

Write-Host "`n[4/4] Configurando senha seed..." -ForegroundColor Yellow
if ([string]::IsNullOrWhiteSpace($SeedPassword)) {
    $seedPasswordSecure = Read-Host "Digite a senha para os usuários seed (ex: Test123!):" -AsSecureString
    $SeedPassword = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToBSTR($seedPasswordSecure)
    )
}

try {
    # Salva a senha seed em arquivo separado (não versionado)
    $SeedPassword | Out-File -FilePath ".env.e2e.seedpassword" -Encoding UTF8 -Force
    Write-Host "Senha seed salva em .env.e2e.seedpassword (gitignored)." -ForegroundColor Green

    # O formato dotenv interpreta # como comentário fora de aspas. Escape o valor
    # para preservar senhas fortes exatamente como foram informadas.
    $escapedSeedPassword = $SeedPassword.Replace("\", "\\").Replace('"', '\"')

    @(
        "E2E_BASE_URL=http://127.0.0.1:5450"
        "E2E_API_URL=http://127.0.0.1:5400"
        "VITE_API_URL=http://127.0.0.1:5400"
        "E2E_TEST_USER_EMAIL=$TestUserEmail"
        "E2E_TEST_USER_PASSWORD=`"$escapedSeedPassword`""
    ) | Out-File -FilePath "src/Prisma.Workspace.Web/.env.e2e.local" -Encoding UTF8 -Force
    Write-Host "Credenciais locais do Playwright salvas em src/Prisma.Workspace.Web/.env.e2e.local (gitignored)." -ForegroundColor Green
} catch {
    Write-Host "Aviso: não foi possível salvar .env.e2e.seedpassword: $_" -ForegroundColor Yellow
}

Write-Host "`n=== Setup concluído ===" -ForegroundColor Cyan
Write-Host "Próximos passos:" -ForegroundColor White
Write-Host "1. Inicie a API com banco E2E: .\scripts\run-api-e2e.ps1" -ForegroundColor Gray
Write-Host "2. Inicie o frontend na porta E2E: npm run dev -- --mode e2e --host 127.0.0.1 --port 5450 (em src/Prisma.Workspace.Web)" -ForegroundColor Gray
Write-Host "3. Execute os testes: npm run e2e (em src/Prisma.Workspace.Web)" -ForegroundColor Gray
