# Promove ou revoga explicitamente uma conta existente no control plane.
# A conexão é fornecida externamente; não usar perfis da organização como origem da autoridade.
param(
    [Parameter(Mandatory = $true)][string]$UserId,
    [switch]$Revoke
)
$ErrorActionPreference = 'Stop'
$platformConnectionString = $env:ConnectionStrings__DefaultConnection
if ([string]::IsNullOrWhiteSpace($platformConnectionString)) { throw 'Configure ConnectionStrings__DefaultConnection externamente.' }
$connection = New-Object System.Data.SqlClient.SqlConnection($platformConnectionString)
try {
    $connection.Open()
    $command = $connection.CreateCommand()
    $command.CommandText = 'UPDATE AspNetUsers SET IsPlatformAdministrator = @flag WHERE Id = @id; SELECT @@ROWCOUNT;'
    [void]$command.Parameters.Add('@id', [System.Data.SqlDbType]::NVarChar, 450)
    $command.Parameters['@id'].Value = $UserId
    [void]$command.Parameters.Add('@flag', [System.Data.SqlDbType]::Bit)
    $command.Parameters['@flag'].Value = -not $Revoke
    $changed = [int]$command.ExecuteScalar()
    if ($changed -ne 1) { throw 'Conta não encontrada; nenhuma autoridade concedida.' }
    Write-Output 'Autoridade de plataforma atualizada para a conta indicada.'
} finally { $connection.Dispose() }
