param(
    [Parameter(Mandatory = $true)]
    [string]$Payload
)

$ErrorActionPreference = 'Stop'
$payloadJson = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($Payload))
$request = ConvertFrom-Json -InputObject $payloadJson
$sourcePath = Join-Path $PSScriptRoot 'WindowsFileOperation.cs'
Add-Type -Path $sourcePath

$sources = @($request.sources | ForEach-Object { [string]$_ })
$result = [Lumina.WindowsShell.FileOperationRunner]::Execute(
    [string]$request.action,
    [string[]]$sources,
    [string]$request.destination,
    [string]$request.newName,
    [bool]$request.permanent,
    [bool]$request.renameOnCollision,
    [long]$request.ownerHandle,
    [bool]$request.addUndoRecord,
    [bool]$request.noConfirmation
)

$resultJson = $result | ConvertTo-Json -Compress
[Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($resultJson))
