$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'

Add-Type -AssemblyName System.Windows.Forms

[Console]::InputEncoding = [Text.UTF8Encoding]::new($false)
[Console]::OutputEncoding = [Text.UTF8Encoding]::new($false)

function New-DropEffectStream([int]$effect) {
  $bytes = [BitConverter]::GetBytes($effect)
  $stream = [IO.MemoryStream]::new()
  $stream.Write($bytes, 0, $bytes.Length)
  $stream.Position = 0
  return $stream
}

function Read-DropEffect($dataObject) {
  if (-not $dataObject -or -not $dataObject.GetDataPresent('Preferred DropEffect')) {
    return 1
  }

  $raw = $dataObject.GetData('Preferred DropEffect')
  if ($raw -is [IO.Stream]) {
    $position = if ($raw.CanSeek) { $raw.Position } else { 0 }
    if ($raw.CanSeek) { $raw.Position = 0 }
    $bytes = [byte[]]::new(4)
    $count = $raw.Read($bytes, 0, 4)
    if ($raw.CanSeek) { $raw.Position = $position }
    if ($count -ge 4) { return [BitConverter]::ToInt32($bytes, 0) }
  }

  if ($raw -is [Array] -and $raw.Length -ge 4) {
    return [BitConverter]::ToInt32([byte[]]$raw, 0)
  }
  return 1
}

function Get-ClipboardDataObject {
  for ($attempt = 0; $attempt -lt 10; $attempt += 1) {
    try {
      return [Windows.Forms.Clipboard]::GetDataObject()
    } catch [Runtime.InteropServices.ExternalException] {
      if ($attempt -eq 9) { throw }
      Start-Sleep -Milliseconds 25
    }
  }
}

function Write-FileClipboard($request) {
  $files = [Collections.Specialized.StringCollection]::new()
  foreach ($filePath in @($request.paths)) {
    [void]$files.Add([string]$filePath)
  }

  $data = [Windows.Forms.DataObject]::new()
  $data.SetFileDropList($files)
  $effect = if ([bool]$request.move) { 2 } else { 1 }
  $data.SetData('Preferred DropEffect', $false, (New-DropEffectStream $effect))
  [Windows.Forms.Clipboard]::SetDataObject($data, $true, 10, 50)

  # Keep the managed source alive as well as asking OLE to persist the data.
  $script:lastClipboardData = $data
  return @{ written = $true }
}

function Read-FileClipboard {
  $data = Get-ClipboardDataObject
  $paths = @()
  if ($data -and $data.GetDataPresent([Windows.Forms.DataFormats]::FileDrop)) {
    $paths = @($data.GetData([Windows.Forms.DataFormats]::FileDrop))
  }
  $effect = Read-DropEffect $data
  return @{ paths = $paths; move = (($effect -band 2) -ne 0) }
}

function Complete-FilePaste($request) {
  $data = Get-ClipboardDataObject
  if (-not $data) { return @{ completed = $false } }

  $effect = if ([bool]$request.move) { 2 } else { 1 }
  $data.SetData('Performed DropEffect', $false, (New-DropEffectStream $effect))
  $data.SetData('Paste Succeeded', $false, (New-DropEffectStream $effect))
  if ($effect -eq 2) {
    [Windows.Forms.Clipboard]::Clear()
    $script:lastClipboardData = $null
  }
  return @{ completed = $true }
}

[Console]::Out.WriteLine('LUMINA_CLIPBOARD_READY')
[Console]::Out.Flush()

while (($line = [Console]::In.ReadLine()) -ne $null) {
  if ([string]::IsNullOrWhiteSpace($line)) { continue }
  try {
    $json = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String($line))
    $request = ConvertFrom-Json -InputObject $json
    $data = switch ([string]$request.action) {
      'write' { Write-FileClipboard $request; break }
      'read' { Read-FileClipboard; break }
      'complete' { Complete-FilePaste $request; break }
      default { throw "Unknown clipboard action: $($request.action)" }
    }
    $response = @{ ok = $true; data = $data }
  } catch {
    $response = @{ ok = $false; error = $_.Exception.Message }
  }

  $responseJson = ConvertTo-Json -InputObject $response -Compress -Depth 5
  $responseBase64 = [Convert]::ToBase64String([Text.Encoding]::UTF8.GetBytes($responseJson))
  [Console]::Out.WriteLine("LUMINA_CLIPBOARD:$responseBase64")
  [Console]::Out.Flush()
}
