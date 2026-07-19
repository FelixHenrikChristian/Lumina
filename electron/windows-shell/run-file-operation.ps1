# Executes one Shell file operation described by a base64 JSON payload on
# stdin (stdin instead of argv: selections with many long paths would
# otherwise overflow the ~32k Windows command line). Protocol on stdout:
#   LUMINA_FILE_PROGRESS:<points,pointsTotal,size,sizeTotal,items,itemsTotal>
#   LUMINA_FILE_RESULT:<base64 JSON>   on success
#   LUMINA_FILE_ERROR:<base64 JSON>    on failure (exit code 1)
$ErrorActionPreference = 'Stop'

function Write-LuminaProtocol([string]$Kind, [object]$Value) {
    $json = $Value | ConvertTo-Json -Compress -Depth 5
    $encoded = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($json))
    [Console]::Out.WriteLine("LUMINA_FILE_${Kind}:$encoded")
    [Console]::Out.Flush()
}

try {
    $payload = [Console]::In.ReadLine()
    if ([string]::IsNullOrWhiteSpace($payload)) {
        throw 'No file-operation payload was provided on stdin.'
    }
    $payloadJson = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($payload.Trim()))
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
        [bool]$request.noConfirmation,
        [string]$request.cancellationPath,
        [bool]$request.reportProgress
    )
    Write-LuminaProtocol 'RESULT' $result
}
catch {
    Write-LuminaProtocol 'ERROR' @{
        Message = [string]$_.Exception.Message
        HResult = [int]$_.Exception.HResult
    }
    exit 1
}
