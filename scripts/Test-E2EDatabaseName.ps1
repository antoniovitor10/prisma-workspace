function Test-E2EDatabaseName {
    [CmdletBinding()]
    param(
        [AllowNull()]
        [AllowEmptyString()]
        [string]$DatabaseName
    )

    if (
        [string]::IsNullOrWhiteSpace($DatabaseName) -or
        $DatabaseName -eq "DetranKanban" -or
        $DatabaseName -eq "DetranKanban_Dev" -or
        -not (
            $DatabaseName -like "DetranKanban_E2E*" -or
            $DatabaseName -like "DetranKanban_MigrationTest_*"
        )
    ) {
        throw "Banco recusado: '$DatabaseName'. O nome deve começar com DetranKanban_E2E ou DetranKanban_MigrationTest_."
    }

    return $true
}
