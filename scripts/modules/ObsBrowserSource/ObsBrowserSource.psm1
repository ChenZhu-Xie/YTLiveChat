Set-StrictMode -Version Latest

$implementationPath = [IO.Path]::GetFullPath(
    (Join-Path $PSScriptRoot "..\..\ObsBrowserSource.ps1"))
. $implementationPath

Export-ModuleMember -Function Complete-BackendLaunch
