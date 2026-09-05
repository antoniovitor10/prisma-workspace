[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$repositoryRoot = (Resolve-Path -LiteralPath (Join-Path $PSScriptRoot '..')).Path
Set-Location -LiteralPath $repositoryRoot

$files = @(git ls-files --cached --others --exclude-standard)
$violations = [System.Collections.Generic.List[string]]::new()

$forbiddenPrefixes = @(
    '.agent-state/',
    '.artifacts/',
    '.runlogs/',
    'docs/entrada/',
    'migracao/'
)

$forbiddenExactPaths = @(
    '.github/workflows/deploy.yml',
    'DEPLOY.md',
    'HANDOFF.md',
    'RODAR-LOCAL.md',
    'docs/context-explorer.html',
    'out1.txt',
    'tools/context-explorer/model.json',
    'tools/context-explorer/web/public/model.json'
)

$forbiddenExtensions = @('.bak', '.docx', '.pptx', '.xlsx', '.7z')

foreach ($file in $files) {
    $normalized = $file.Replace('\', '/')

    if ($forbiddenPrefixes | Where-Object { $normalized.StartsWith($_, [StringComparison]::OrdinalIgnoreCase) }) {
        $violations.Add("prefix:$normalized")
    }

    if ($forbiddenExactPaths -contains $normalized) {
        $violations.Add("path:$normalized")
    }

    if ($forbiddenExtensions -contains [IO.Path]::GetExtension($normalized).ToLowerInvariant()) {
        $violations.Add("extension:$normalized")
    }

    $name = [IO.Path]::GetFileName($normalized)
    if (($name -eq '.env' -or $name.StartsWith('.env.', [StringComparison]::OrdinalIgnoreCase)) -and
        $name -ne '.env.example') {
        $violations.Add("environment:$normalized")
    }

    if ($normalized -match '(^|/)e2e/\.auth/') {
        $violations.Add("browser-auth:$normalized")
    }
}

if ($violations.Count -gt 0) {
    Write-Error ("Snapshot público reprovado:`n- " + (($violations | Sort-Object -Unique) -join "`n- "))
}

$gitleaks = Get-Command gitleaks -ErrorAction SilentlyContinue
if (-not $gitleaks) {
    Write-Error 'gitleaks não está instalado; a auditoria não pode ser considerada concluída.'
}

& $gitleaks.Source dir $repositoryRoot --no-banner --redact=100 --exit-code 1
if ($LASTEXITCODE -ne 0) {
    throw "gitleaks encontrou conteúdo suspeito (código $LASTEXITCODE)."
}

Write-Host "Snapshot aprovado: $($files.Count) arquivos candidatos e zero achados do gitleaks."
