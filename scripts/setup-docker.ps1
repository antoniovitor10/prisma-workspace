param(
    [switch]$AcceptSqlServerEula,
    [switch]$NoStart
)

$ErrorActionPreference = 'Stop'
$workspace = (Resolve-Path (Join-Path $PSScriptRoot '..')).Path
$envPath = Join-Path $workspace '.env'

if (-not $AcceptSqlServerEula) {
    throw 'Confirme a licença do SQL Server com -AcceptSqlServerEula. Consulte docs/installation/docker.md.'
}
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    throw 'Docker não encontrado no PATH.'
}
docker compose version | Out-Null
if ($LASTEXITCODE -ne 0) { throw 'Docker Compose v2 não está disponível.' }
if (Test-Path $envPath) {
    throw '.env já existe. Preserve seus segredos ou remova o arquivo conscientemente antes de gerar outro.'
}

function New-HexSecret([int]$ByteCount) {
    $bytes = New-Object byte[] $ByteCount
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) }
    finally { $generator.Dispose() }
    return ([BitConverter]::ToString($bytes)).Replace('-', '')
}

function New-Base64UrlSecret([int]$ByteCount) {
    $bytes = New-Object byte[] $ByteCount
    $generator = [System.Security.Cryptography.RandomNumberGenerator]::Create()
    try { $generator.GetBytes($bytes) }
    finally { $generator.Dispose() }
    return [Convert]::ToBase64String($bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')
}

$sqlPassword = 'Pr1!' + (New-HexSecret 24)
$jwtKey = New-HexSecret 48
$setupToken = New-Base64UrlSecret 32
$content = @(
    "MSSQL_SA_PASSWORD=$sqlPassword"
    "PRISMA_JWT_KEY=$jwtKey"
    'PRISMA_SETUP_ENABLED=true'
    "PRISMA_SETUP_TOKEN=$setupToken"
    'MSSQL_PID=Developer'
    'PRISMA_DB_NAME=PrismaWorkspace'
    'PRISMA_HTTP_PORT=8080'
) -join [Environment]::NewLine
[System.IO.File]::WriteAllText($envPath, $content + [Environment]::NewLine)
Write-Host 'Arquivo .env criado com segredos aleatórios e mantido fora do Git.'

if (-not $NoStart) {
    Push-Location $workspace
    try {
        docker compose up --build --detach --wait
        if ($LASTEXITCODE -ne 0) { throw 'A instalação Docker não ficou saudável.' }
    }
    finally { Pop-Location }
    Write-Host 'Prisma WorkSpace disponível em http://localhost:8080'
}
